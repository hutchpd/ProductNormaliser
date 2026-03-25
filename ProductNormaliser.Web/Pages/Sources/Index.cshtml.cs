using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Models;
using ProductNormaliser.Web.Services;
using System.ComponentModel.DataAnnotations;

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

    [BindProperty]
    public RegisterSourceInput Registration { get; set; } = new();

    [BindProperty]
    public DiscoverSourceCandidatesInput CandidateDiscovery { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public string? CandidateDiscoveryErrorMessage { get; private set; }

    public SourceCandidateDiscoveryResponseDto? CandidateDiscoveryResult { get; private set; }

    public IReadOnlyList<CategoryMetadataDto> Categories { get; private set; } = [];

    public IReadOnlyList<SourceDto> AllSources { get; private set; } = [];

    public IReadOnlyList<SourceDto> Sources { get; private set; } = [];

    public int TotalSources { get; private set; }

    public int ReadySources => Sources.Count(source => string.Equals(source.Readiness.Status, "Ready", StringComparison.OrdinalIgnoreCase));

    public int AttentionSources => Sources.Count(source => string.Equals(source.Health.Status, "Attention", StringComparison.OrdinalIgnoreCase));

    public int DiscoveryBacklogSources => Sources.Count(source => source.DiscoveryQueueDepth > 0);

    public int ActiveDiscoverySources => Sources.Count(source => source.LastDiscoveryUtc is not null || source.ConfirmedProductUrlsLast24Hours > 0);

    public int RegistryEnabledSources => AllSources.Count(source => source.IsEnabled);

    public int RegistryDiscoveryConfiguredSources => AllSources.Count(HasDiscoveryScaffold);

    public int RegistryBootReadySources => AllSources.Count(IsBootReady);

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
            new HeroMetricModel { Label = "Discovery active", Value = ActiveDiscoverySources.ToString() }
        ]
    };

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostRegisterAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        try
        {
            var source = await adminApiClient.RegisterSourceAsync(new RegisterSourceRequest
            {
                SourceId = Registration.SourceId,
                DisplayName = Registration.DisplayName,
                BaseUrl = Registration.BaseUrl,
                Description = Registration.Description,
                IsEnabled = Registration.IsEnabled,
                SupportedCategoryKeys = Registration.CategoryKeys
            }, cancellationToken);

            StatusMessage = $"Registered source '{source.DisplayName}'. Startup discovery defaults were applied automatically.";
            return RedirectToPage("/Sources/Details", new { sourceId = source.SourceId });
        }
        catch (AdminApiValidationException exception)
        {
            foreach (var entry in exception.Errors)
            {
                foreach (var message in entry.Value)
                {
                    ModelState.AddModelError(string.Empty, message);
                }
            }
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to register source from sources index.");
            ErrorMessage = exception.Message;
        }

        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostDiscoverCandidatesAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);

        var categoryKeys = NormalizeValues(CandidateDiscovery.CategoryKeys);
        if (categoryKeys.Count == 0 && !string.IsNullOrWhiteSpace(CategoryKey))
        {
            categoryKeys = [CategoryKey];
        }

        if (categoryKeys.Count == 0)
        {
            ModelState.AddModelError($"{nameof(CandidateDiscovery)}.{nameof(CandidateDiscovery.CategoryKeys)}", "Choose at least one category before discovering source candidates.");
            return Page();
        }

        CandidateDiscovery.CategoryKeys = categoryKeys;

        try
        {
            CandidateDiscoveryResult = await adminApiClient.DiscoverSourceCandidatesAsync(new DiscoverSourceCandidatesRequest
            {
                CategoryKeys = categoryKeys,
                Locale = NormalizeOptionalText(CandidateDiscovery.Locale),
                Market = NormalizeOptionalText(CandidateDiscovery.Market),
                BrandHints = ParseDelimitedValues(CandidateDiscovery.BrandHints),
                MaxCandidates = NormalizeMaxCandidates(CandidateDiscovery.MaxCandidates)
            }, cancellationToken);
        }
        catch (AdminApiValidationException exception)
        {
            foreach (var entry in exception.Errors)
            {
                foreach (var message in entry.Value)
                {
                    ModelState.AddModelError(string.Empty, message);
                }
            }
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to discover source candidates from sources index.");
            CandidateDiscoveryErrorMessage = exception.Message;
        }

        return Page();
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

    public bool HasDiscoveryScaffold(SourceDto source)
    {
        return source.DiscoveryProfile.CategoryEntryPages.Count > 0
            || source.DiscoveryProfile.SitemapHints.Count > 0
            || source.DiscoveryProfile.ProductUrlPatterns.Count > 0
            || source.DiscoveryProfile.ListingUrlPatterns.Count > 0;
    }

    public bool IsBootReady(SourceDto source)
    {
        return source.IsEnabled
            && HasDiscoveryScaffold(source)
            && source.Readiness.CrawlableCategoryCount > 0;
    }

    public string GetSetupSummary(SourceDto source)
    {
        if (!source.IsEnabled)
        {
            return "Disabled until explicitly enabled.";
        }

        if (!HasDiscoveryScaffold(source))
        {
            return "No discovery seed profile is configured yet.";
        }

        if (source.Readiness.CrawlableCategoryCount == 0)
        {
            return "Assigned categories are not crawl-ready yet.";
        }

        return "Ready to seed discovery from startup defaults or an edited profile.";
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
            AllSources = allSources;
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

            if (Registration.CategoryKeys.Count == 0 && !string.IsNullOrWhiteSpace(CategoryKey))
            {
                Registration.CategoryKeys = [CategoryKey];
            }

            if (CandidateDiscovery.CategoryKeys.Count == 0 && !string.IsNullOrWhiteSpace(CategoryKey))
            {
                CandidateDiscovery.CategoryKeys = [CategoryKey];
            }
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to load sources page data.");
            ErrorMessage = exception.Message;
            Categories = [];
            AllSources = [];
            Sources = [];
        }
    }

    private static List<string> NormalizeValues(IEnumerable<string>? values)
    {
        return (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ParseDelimitedValues(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return NormalizeValues(value
            .Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int NormalizeMaxCandidates(int value)
    {
        if (value <= 0)
        {
            return 10;
        }

        return Math.Min(25, value);
    }

    public sealed class RegisterSourceInput
    {
        [Required]
        [Display(Name = "Source id")]
        public string SourceId { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Display name")]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [Url]
        [Display(Name = "Base URL")]
        public string BaseUrl { get; set; } = string.Empty;

        [Display(Name = "Description")]
        public string? Description { get; set; }

        public bool IsEnabled { get; set; } = true;

        public List<string> CategoryKeys { get; set; } = [];
    }

    public sealed class DiscoverSourceCandidatesInput
    {
        [Display(Name = "Categories")]
        public List<string> CategoryKeys { get; set; } = [];

        [Display(Name = "Locale")]
        public string? Locale { get; set; }

        [Display(Name = "Market")]
        public string? Market { get; set; }

        [Display(Name = "Brand hints")]
        public string? BrandHints { get; set; }

        [Range(1, 25)]
        [Display(Name = "Max candidates")]
        public int MaxCandidates { get; set; } = 10;
    }
}