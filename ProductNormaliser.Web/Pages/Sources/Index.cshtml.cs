using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Models;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Pages.Sources;

public sealed class IndexModel(
    IProductNormaliserAdminApiClient adminApiClient,
    ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "category")]
    public string? CategoryKey { get; set; }

    [BindProperty(SupportsGet = true, Name = "search")]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true, Name = "enabled")]
    public bool? Enabled { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public IReadOnlyList<CategoryMetadataDto> Categories { get; private set; } = [];

    public IReadOnlyList<SourceDto> Sources { get; private set; } = [];

    public int TotalSources { get; private set; }

    public int ReadySources => Sources.Count(source => string.Equals(source.Readiness.Status, "Ready", StringComparison.OrdinalIgnoreCase));

    public int AttentionSources => Sources.Count(source => string.Equals(source.Health.Status, "Attention", StringComparison.OrdinalIgnoreCase));

    public PageHeroModel Hero => new()
    {
        Eyebrow = "Source Registry",
        Title = "Manage enabled hosts and source readiness",
        Description = "Review each crawl host’s assigned category coverage, throttling posture, readiness, health indicators, and recent crawl activity before changing source state.",
        Metrics =
        [
            new HeroMetricModel { Label = "Filtered", Value = Sources.Count.ToString() },
            new HeroMetricModel { Label = "Total", Value = TotalSources.ToString() },
            new HeroMetricModel { Label = "Enabled", Value = Sources.Count(source => source.IsEnabled).ToString() },
            new HeroMetricModel { Label = "Ready", Value = ReadySources.ToString() }
        ]
    };

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostToggleAsync(string sourceId, bool currentlyEnabled, string? category, string? search, bool? enabled, CancellationToken cancellationToken)
    {
        CategoryKey = category;
        Search = search;
        Enabled = enabled;

        try
        {
            if (currentlyEnabled)
            {
                await adminApiClient.DisableSourceAsync(sourceId, cancellationToken);
                StatusMessage = $"Disabled source '{sourceId}'.";
            }
            else
            {
                await adminApiClient.EnableSourceAsync(sourceId, cancellationToken);
                StatusMessage = $"Enabled source '{sourceId}'.";
            }

            return RedirectToPage(new { category = CategoryKey, search = Search, enabled = Enabled });
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to toggle source {SourceId} from sources index.", sourceId);
            ErrorMessage = exception.Message;
        }

        await LoadAsync(cancellationToken);
        return Page();
    }

    public IReadOnlyList<string> GetAssignedCategoryLabels(SourceDto source)
    {
        if (source.SupportedCategoryKeys.Count == 0)
        {
            return ["No categories assigned"];
        }

        var categoryLookup = Categories.ToDictionary(category => category.CategoryKey, StringComparer.OrdinalIgnoreCase);
        return source.SupportedCategoryKeys
            .Select(categoryKey => categoryLookup.TryGetValue(categoryKey, out var category) ? category.DisplayName : categoryKey)
            .ToArray();
    }

    public string BuildIntelligenceUrl(SourceDto source)
    {
        var categoryKey = !string.IsNullOrWhiteSpace(CategoryKey)
            ? CategoryKey
            : source.SupportedCategoryKeys.FirstOrDefault();

        return string.IsNullOrWhiteSpace(categoryKey)
            ? "/Sources/Intelligence"
            : $"/Sources/Intelligence?category={Uri.EscapeDataString(categoryKey)}&source={Uri.EscapeDataString(source.DisplayName)}";
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var categoriesTask = adminApiClient.GetCategoriesAsync(cancellationToken);
            var sourcesTask = adminApiClient.GetSourcesAsync(cancellationToken);
            await Task.WhenAll(categoriesTask, sourcesTask);

            Categories = InteractiveCategoryFilter.Apply(categoriesTask.Result);
            var categoryContext = CategoryContextStateFactory.Resolve(
                Categories,
                CategoryKey,
                null,
                PageContext?.HttpContext?.Request.Cookies[CategoryContextState.CookieName]);
            CategoryKey = categoryContext.PrimaryCategoryKey;

            var allSources = sourcesTask.Result.OrderBy(source => source.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
            TotalSources = allSources.Length;

            IEnumerable<SourceDto> filtered = allSources;
            if (!string.IsNullOrWhiteSpace(CategoryKey))
            {
                filtered = filtered.Where(source => source.SupportedCategoryKeys.Contains(CategoryKey, StringComparer.OrdinalIgnoreCase));
            }

            if (Enabled.HasValue)
            {
                filtered = filtered.Where(source => source.IsEnabled == Enabled.Value);
            }

            if (!string.IsNullOrWhiteSpace(Search))
            {
                filtered = filtered.Where(source =>
                    source.SourceId.Contains(Search, StringComparison.OrdinalIgnoreCase)
                    || source.DisplayName.Contains(Search, StringComparison.OrdinalIgnoreCase)
                    || source.Host.Contains(Search, StringComparison.OrdinalIgnoreCase));
            }

            Sources = filtered.ToArray();
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to load sources page data.");
            ErrorMessage = exception.Message;
            Categories = [];
            Sources = [];
        }
    }
}