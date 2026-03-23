using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Models;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Pages.Sources;

public sealed class IntelligenceModel(
    IProductNormaliserAdminApiClient adminApiClient,
    ILogger<IntelligenceModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "category")]
    public string? CategoryKey { get; set; }

    [BindProperty(SupportsGet = true, Name = "source")]
    public string? SourceName { get; set; }

    public string? ErrorMessage { get; private set; }

    public IReadOnlyList<CategoryMetadataDto> Categories { get; private set; } = [];

    public IReadOnlyList<SourceDto> Sources { get; private set; } = [];

    public IReadOnlyList<SourceQualityScoreDto> SourceQualityScores { get; private set; } = [];

    public IReadOnlyList<SourceQualitySnapshotDto> SourceHistory { get; private set; } = [];

    public IReadOnlyList<SourceAttributeDisagreementDto> SourceDisagreements { get; private set; } = [];

    public CategoryMetadataDto? SelectedCategory => Categories.FirstOrDefault(category => string.Equals(category.CategoryKey, CategoryKey, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<string> AvailableSourceNames => Sources
        .Where(source => string.IsNullOrWhiteSpace(CategoryKey) || source.SupportedCategoryKeys.Contains(CategoryKey, StringComparer.OrdinalIgnoreCase))
        .Select(source => source.DisplayName)
        .Concat(SourceQualityScores.Select(score => score.SourceName))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(sourceName => sourceName, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public string? EffectiveSourceName { get; private set; }

    public IReadOnlyList<SourceQualitySnapshotDto> TrendSnapshots => SourceHistory
        .OrderBy(snapshot => snapshot.TimestampUtc)
        .ToArray();

    public SourceQualitySnapshotDto? LatestSnapshot => TrendSnapshots.LastOrDefault();

    public bool IsAwaitingSelection => string.IsNullOrWhiteSpace(CategoryKey);

    public bool IsEmpty => !IsAwaitingSelection
        && SourceQualityScores.Count == 0
        && SourceHistory.Count == 0
        && SourceDisagreements.Count == 0;

    public IReadOnlyList<SourceQualityScoreDto> SourceOverview => SourceQualityScores
        .OrderByDescending(score => score.QualityScore)
        .ThenByDescending(score => score.SourceProductCount)
        .Take(8)
        .ToArray();

    public IReadOnlyList<SourceAttributeDisagreementDto> DisagreementHotspots => SourceDisagreements
        .OrderByDescending(item => item.DisagreementRate)
        .ThenByDescending(item => item.TotalComparisons)
        .ThenBy(item => item.AttributeKey, StringComparer.OrdinalIgnoreCase)
        .Take(10)
        .ToArray();

    public PageHeroModel Hero => new()
    {
        Eyebrow = "Source Intelligence",
        Title = SelectedCategory is null ? "Source trust and change intelligence" : $"{SelectedCategory.DisplayName} source intelligence",
        Description = SelectedCategory is null
            ? "Choose a category to inspect trust, disagreement, completeness, and change activity for the sources that actually participate in that category."
            : "Compare source quality, then drill into one source to inspect trust over time, disagreement hotspots, completeness, and change activity trends.",
        Metrics =
        [
            new HeroMetricModel { Label = "Sources", Value = SourceQualityScores.Count.ToString() },
            new HeroMetricModel { Label = "Focused source", Value = EffectiveSourceName ?? "None selected" },
            new HeroMetricModel { Label = "Latest trust", Value = LatestSnapshot is null ? "0%" : AnalyticsPresentation.FormatPercent(LatestSnapshot.HistoricalTrustScore) },
            new HeroMetricModel { Label = "Latest coverage", Value = LatestSnapshot is null ? "0%" : AnalyticsPresentation.FormatPercent(LatestSnapshot.AttributeCoverage) }
        ]
    };

    public async Task OnGetAsync(CancellationToken cancellationToken)
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
            Sources = sourcesTask.Result
                .OrderBy(source => source.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            CategoryKey = categoryContext.PrimaryCategoryKey;

            if (IsAwaitingSelection)
            {
                return;
            }

            SourceQualityScores = await adminApiClient.GetSourceQualityScoresAsync(CategoryKey!, cancellationToken);

            EffectiveSourceName = AvailableSourceNames.Contains(SourceName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                ? SourceName
                : SourceQualityScores.FirstOrDefault()?.SourceName;

            var historyTask = adminApiClient.GetSourceHistoryAsync(CategoryKey!, EffectiveSourceName, cancellationToken);
            var disagreementsTask = adminApiClient.GetSourceDisagreementsAsync(CategoryKey!, EffectiveSourceName, cancellationToken);
            await Task.WhenAll(historyTask, disagreementsTask);

            SourceHistory = historyTask.Result;
            SourceDisagreements = disagreementsTask.Result;
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to load source intelligence page for {CategoryKey}/{SourceName}.", CategoryKey, SourceName);
            ErrorMessage = exception.Message;
            SourceQualityScores = [];
            SourceHistory = [];
            SourceDisagreements = [];
        }
    }
}
