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
    private const string RoutePath = "/Sources/Intelligence";
    private static readonly int[] SupportedTimeRanges = [7, 30, 90, 180];

    [BindProperty(SupportsGet = true, Name = "category")]
    public string? CategoryKey { get; set; }

    [BindProperty(SupportsGet = true, Name = "source")]
    public string? SourceName { get; set; }

    [BindProperty(SupportsGet = true, Name = "view")]
    public string? SavedViewId { get; set; }

    [BindProperty(SupportsGet = true, Name = "range")]
    public int? TimeRangeDays { get; set; }

    [BindProperty]
    public string SaveViewName { get; set; } = string.Empty;

    [BindProperty]
    public string? SaveViewDescription { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public string? WorkflowMessage { get; private set; }

    public IReadOnlyList<CategoryMetadataDto> Categories { get; private set; } = [];

    public IReadOnlyList<SourceDto> Sources { get; private set; } = [];

    public IReadOnlyList<SourceQualityScoreDto> SourceQualityScores { get; private set; } = [];

    public IReadOnlyList<SourceQualitySnapshotDto> CategoryHistory { get; private set; } = [];

    public IReadOnlyList<SourceAttributeDisagreementDto> CategoryDisagreements { get; private set; } = [];

    public IReadOnlyList<SourceIntelligenceMetricModel> SourceMetrics { get; private set; } = [];

    public IReadOnlyList<SupportMatrixCategoryModel> SupportMatrixCategories { get; private set; } = [];

    public IReadOnlyList<SupportMatrixRowModel> SupportMatrixRows { get; private set; } = [];

    public IReadOnlyList<SourceIntelligenceHighlightModel> TriageHighlights { get; private set; } = [];

    public IReadOnlyList<SavedAnalystWorkflowCardModel> SavedWorkflows { get; private set; } = [];

    public CategoryMetadataDto? SelectedCategory => Categories.FirstOrDefault(category => string.Equals(category.CategoryKey, CategoryKey, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<string> AvailableSourceNames => Sources
        .Where(source => string.IsNullOrWhiteSpace(CategoryKey) || source.SupportedCategoryKeys.Contains(CategoryKey, StringComparer.OrdinalIgnoreCase))
        .Select(source => source.DisplayName)
        .Concat(SourceQualityScores.Select(score => score.SourceName))
        .Concat(CategoryHistory.Select(snapshot => snapshot.SourceName))
        .Concat(CategoryDisagreements.Select(item => item.SourceName))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(sourceName => sourceName, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public IReadOnlyList<TimeRangeOptionModel> TimeRangeOptions => SupportedTimeRanges
        .Select(days => new TimeRangeOptionModel(days, days switch
        {
            7 => "Last 7 days",
            30 => "Last 30 days",
            90 => "Last 90 days",
            180 => "Last 180 days",
            _ => $"Last {days} days"
        }))
        .ToArray();

    public int EffectiveTimeRangeDays { get; private set; } = 30;

    public string? EffectiveSourceName { get; private set; }

    public IReadOnlyList<SourceQualitySnapshotDto> SourceHistory => CategoryHistory
        .Where(snapshot => string.Equals(snapshot.SourceName, EffectiveSourceName, StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(snapshot => snapshot.TimestampUtc)
        .ToArray();

    public IReadOnlyList<SourceAttributeDisagreementDto> SourceDisagreements => CategoryDisagreements
        .Where(item => string.Equals(item.SourceName, EffectiveSourceName, StringComparison.OrdinalIgnoreCase))
        .ToArray();

    public IReadOnlyList<SourceQualitySnapshotDto> TrendSnapshots => SourceHistory
        .OrderBy(snapshot => snapshot.TimestampUtc)
        .ToArray();

    public SourceQualitySnapshotDto? LatestSnapshot => TrendSnapshots.LastOrDefault();

    public bool IsAwaitingSelection => string.IsNullOrWhiteSpace(CategoryKey);

    public bool IsEmpty => !IsAwaitingSelection
        && SourceQualityScores.Count == 0
        && CategoryHistory.Count == 0
        && CategoryDisagreements.Count == 0;

    public IReadOnlyList<SourceIntelligenceMetricModel> SourceOverview => SourceMetrics
        .OrderByDescending(score => score.ValueScore)
        .ThenByDescending(score => score.QualityScore)
        .Take(8)
        .ToArray();

    public IReadOnlyList<SourceIntelligenceMetricModel> WeakSources => SourceMetrics
        .OrderByDescending(score => score.RiskScore)
        .ThenBy(score => score.ValueScore)
        .Take(5)
        .ToArray();

    public IReadOnlyList<SourceIntelligenceMetricModel> HighValueSources => SourceMetrics
        .OrderByDescending(score => score.ValueScore)
        .ThenBy(score => score.RiskScore)
        .Take(5)
        .ToArray();

    public IReadOnlyList<SourceIntelligenceMetricModel> ChangeLeaders => SourceMetrics
        .Where(metric => metric.HasHistory)
        .OrderByDescending(metric => metric.LatestChangeActivity)
        .ThenByDescending(metric => metric.HotspotCount)
        .Take(8)
        .ToArray();

    public IReadOnlyList<SourceIntelligenceMetricModel> CoverageLeaders => SourceMetrics
        .OrderByDescending(metric => metric.LatestCoveragePercent)
        .ThenByDescending(metric => metric.QualityScore)
        .Take(8)
        .ToArray();

    public IReadOnlyList<SourceAttributeDisagreementDto> DisagreementHotspots => CategoryDisagreements
        .OrderByDescending(item => item.DisagreementRate)
        .ThenByDescending(item => item.TotalComparisons)
        .ThenBy(item => item.AttributeKey, StringComparer.OrdinalIgnoreCase)
        .Take(10)
        .ToArray();

    public string TimeRangeLabel => TimeRangeOptions.First(option => option.Days == EffectiveTimeRangeDays).Label;

    public PageHeroModel Hero => new()
    {
        Eyebrow = "Source Intelligence",
        Title = SelectedCategory is null ? "Source trust and change intelligence" : $"{SelectedCategory.DisplayName} source intelligence",
        Description = SelectedCategory is null
            ? "Choose a category to inspect trust, disagreement, completeness, and change activity for the sources that actually participate in that category."
            : $"Compare source quality, risk, and support breadth across {TimeRangeLabel.ToLowerInvariant()}, then drill into one source to inspect trust over time and disagreement hotspots.",
        Metrics =
        [
            new HeroMetricModel { Label = "Sources", Value = SourceMetrics.Count.ToString() },
            new HeroMetricModel { Label = "Focused source", Value = EffectiveSourceName ?? "Highest-value source" },
            new HeroMetricModel { Label = "Time window", Value = TimeRangeLabel },
            new HeroMetricModel { Label = "Latest trust", Value = LatestSnapshot is null ? "0%" : AnalyticsPresentation.FormatPercent(LatestSnapshot.HistoricalTrustScore) },
            new HeroMetricModel { Label = "Weak watchlist", Value = WeakSources.Count == 0 ? "0" : WeakSources.Count(metric => metric.RiskScore >= 45m).ToString() }
        ]
    };

    public string GetTrendDeltaText(SourceIntelligenceMetricModel metric)
    {
        if (metric.TrendDelta == 0m)
        {
            return "Flat";
        }

        var prefix = metric.TrendDelta > 0m ? "+" : "-";
        return $"{prefix}{AnalyticsPresentation.FormatPercent(decimal.Abs(metric.TrendDelta))}";
    }

    public string GetTrendDeltaTone(SourceIntelligenceMetricModel metric)
    {
        if (metric.TrendDelta >= 5m)
        {
            return "completed";
        }

        if (metric.TrendDelta <= -5m)
        {
            return "danger";
        }

        return "neutral";
    }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            EffectiveTimeRangeDays = NormalizeTimeRange(TimeRangeDays);
            TimeRangeDays = EffectiveTimeRangeDays;

            var categoriesTask = adminApiClient.GetCategoriesAsync(cancellationToken);
            var sourcesTask = adminApiClient.GetSourcesAsync(cancellationToken);
            var workflowsTask = adminApiClient.GetAnalystWorkflowsAsync(routePath: RoutePath, cancellationToken: cancellationToken);
            await Task.WhenAll(categoriesTask, sourcesTask, workflowsTask);

            Categories = InteractiveCategoryFilter.Apply(categoriesTask.Result);
            var workflowDefinitions = workflowsTask.Result;
            var restoredWorkflow = await ResolveSavedWorkflowAsync(workflowDefinitions, cancellationToken);
            ApplySavedWorkflow(restoredWorkflow);
            var categoryContext = CategoryContextStateFactory.Resolve(
                Categories,
                CategoryKey,
                null,
                PageContext?.HttpContext?.Request.Cookies[CategoryContextState.CookieName]);
            Sources = sourcesTask.Result
                .OrderBy(source => source.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (restoredWorkflow is not null && !string.IsNullOrWhiteSpace(restoredWorkflow.PrimaryCategoryKey) && !Categories.Any(category => string.Equals(category.CategoryKey, restoredWorkflow.PrimaryCategoryKey, StringComparison.OrdinalIgnoreCase)))
            {
                WorkflowMessage = $"Saved view '{restoredWorkflow.Name}' referenced missing category '{restoredWorkflow.PrimaryCategoryKey}'. Showing the default active category instead.";
            }

            CategoryKey = categoryContext.PrimaryCategoryKey;
            SavedWorkflows = AnalystWorkspacePresentation.BuildWorkflowCards(
                workflowDefinitions,
                Categories,
                SavedViewId,
                AnalystWorkspacePresentation.WorkflowTypeSourceReviewQueue,
                AnalystWorkspacePresentation.WorkflowTypeSelectedCategories);

            if (IsAwaitingSelection)
            {
                return;
            }

            var scoresTask = adminApiClient.GetSourceQualityScoresAsync(CategoryKey!, cancellationToken);
            var historyTask = adminApiClient.GetSourceHistoryAsync(CategoryKey!, null, EffectiveTimeRangeDays, cancellationToken);
            var disagreementsTask = adminApiClient.GetSourceDisagreementsAsync(CategoryKey!, null, EffectiveTimeRangeDays, cancellationToken);
            await Task.WhenAll(scoresTask, historyTask, disagreementsTask);

            SourceQualityScores = scoresTask.Result;
            CategoryHistory = historyTask.Result;
            CategoryDisagreements = disagreementsTask.Result;

            SourceMetrics = BuildSourceMetrics();
            EffectiveSourceName = ResolveEffectiveSourceName();
            SupportMatrixCategories = Categories.Take(8)
                .Select(category => new SupportMatrixCategoryModel(
                    category.CategoryKey,
                    category.DisplayName,
                    string.Equals(category.CategoryKey, CategoryKey, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            SupportMatrixRows = BuildSupportMatrixRows();
            TriageHighlights = BuildTriageHighlights();
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to load source intelligence page for {CategoryKey}/{SourceName}.", CategoryKey, SourceName);
            ErrorMessage = exception.Message;
            SourceQualityScores = [];
            CategoryHistory = [];
            CategoryDisagreements = [];
            SourceMetrics = [];
            SupportMatrixCategories = [];
            SupportMatrixRows = [];
            TriageHighlights = [];
        }
    }

    public async Task<IActionResult> OnPostSaveViewAsync(string workflowType, CancellationToken cancellationToken)
    {
        try
        {
            var effectiveRange = NormalizeTimeRange(TimeRangeDays);
            var workflow = await adminApiClient.SaveAnalystWorkflowAsync(new UpsertAnalystWorkflowRequest
            {
                Id = SavedViewId,
                Name = string.IsNullOrWhiteSpace(SaveViewName) ? $"{(string.IsNullOrWhiteSpace(CategoryKey) ? "Category" : CategoryKey)} source queue" : SaveViewName.Trim(),
                Description = string.IsNullOrWhiteSpace(SaveViewDescription) ? null : SaveViewDescription.Trim(),
                WorkflowType = workflowType,
                RoutePath = RoutePath,
                PrimaryCategoryKey = CategoryKey,
                SelectedCategoryKeys = string.IsNullOrWhiteSpace(CategoryKey) ? [] : [CategoryKey],
                State = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["category"] = CategoryKey ?? string.Empty,
                    ["source"] = SourceName ?? string.Empty,
                    ["range"] = effectiveRange.ToString()
                }
            }, cancellationToken);
            StatusMessage = $"Saved analyst view '{workflow.Name}'.";
            return RedirectToPage(new { category = CategoryKey, source = SourceName, range = effectiveRange, view = workflow.Id });
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to save source workflow for {CategoryKey}/{SourceName}.", CategoryKey, SourceName);
            ErrorMessage = exception.Message;
            await OnGetAsync(cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteViewAsync(string workflowId, CancellationToken cancellationToken)
    {
        try
        {
            await adminApiClient.DeleteAnalystWorkflowAsync(workflowId, cancellationToken);
            StatusMessage = "Deleted analyst view.";
            return RedirectToPage(new { category = CategoryKey, source = SourceName, range = EffectiveTimeRangeDays });
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to delete source workflow {WorkflowId}.", workflowId);
            ErrorMessage = exception.Message;
            await OnGetAsync(cancellationToken);
            return Page();
        }
    }

    private async Task<AnalystWorkflowDto?> ResolveSavedWorkflowAsync(IReadOnlyList<AnalystWorkflowDto> workflows, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(SavedViewId))
        {
            return null;
        }

        var workflow = workflows.FirstOrDefault(item => string.Equals(item.Id, SavedViewId, StringComparison.OrdinalIgnoreCase))
            ?? await adminApiClient.GetAnalystWorkflowAsync(SavedViewId, cancellationToken);
        if (workflow is null)
        {
            WorkflowMessage = "Saved view was not found.";
        }

        return workflow;
    }

    private void ApplySavedWorkflow(AnalystWorkflowDto? workflow)
    {
        if (workflow is null)
        {
            return;
        }

        CategoryKey = workflow.State.TryGetValue("category", out var category) ? category : CategoryKey;
        SourceName = workflow.State.TryGetValue("source", out var source) ? source : SourceName;
        TimeRangeDays = workflow.State.TryGetValue("range", out var range) && int.TryParse(range, out var parsedRange)
            ? parsedRange
            : TimeRangeDays;
    }

    private int NormalizeTimeRange(int? days)
    {
        return SupportedTimeRanges.Contains(days ?? 0)
            ? days!.Value
            : 30;
    }

    private string? ResolveEffectiveSourceName()
    {
        if (AvailableSourceNames.Contains(SourceName ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            return SourceName;
        }

        return SourceMetrics.FirstOrDefault()?.SourceName
            ?? SourceQualityScores.FirstOrDefault()?.SourceName
            ?? Sources.FirstOrDefault(source => source.SupportedCategoryKeys.Contains(CategoryKey ?? string.Empty, StringComparer.OrdinalIgnoreCase))?.DisplayName;
    }

    private IReadOnlyList<SourceIntelligenceMetricModel> BuildSourceMetrics()
    {
        var relevantSourceNames = Sources
            .Where(source => string.IsNullOrWhiteSpace(CategoryKey) || source.SupportedCategoryKeys.Contains(CategoryKey!, StringComparer.OrdinalIgnoreCase))
            .Select(source => source.DisplayName)
            .Concat(SourceQualityScores.Select(score => score.SourceName))
            .Concat(CategoryHistory.Select(snapshot => snapshot.SourceName))
            .Concat(CategoryDisagreements.Select(item => item.SourceName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var sourceCount = Math.Max(1, Categories.Count);
        var metrics = new List<SourceIntelligenceMetricModel>(relevantSourceNames.Length);

        foreach (var sourceName in relevantSourceNames)
        {
            var source = Sources.FirstOrDefault(item => string.Equals(item.DisplayName, sourceName, StringComparison.OrdinalIgnoreCase));
            var score = SourceQualityScores.FirstOrDefault(item => string.Equals(item.SourceName, sourceName, StringComparison.OrdinalIgnoreCase));
            var sourceSnapshots = CategoryHistory
                .Where(item => string.Equals(item.SourceName, sourceName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.TimestampUtc)
                .ToArray();
            var latestSnapshot = sourceSnapshots.LastOrDefault();
            var earliestSnapshot = sourceSnapshots.FirstOrDefault();
            var disagreements = CategoryDisagreements
                .Where(item => string.Equals(item.SourceName, sourceName, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var qualityScore = score?.QualityScore ?? latestSnapshot?.HistoricalTrustScore ?? source?.Health.TrustScore ?? 0m;
            var latestTrustScore = latestSnapshot?.HistoricalTrustScore ?? source?.Health.TrustScore ?? qualityScore;
            var latestCoveragePercent = latestSnapshot?.AttributeCoverage ?? score?.CoveragePercent ?? source?.Health.CoveragePercent ?? 0m;
            var latestAgreementPercent = score?.AgreementPercent ?? latestSnapshot?.AgreementRate ?? 0m;
            var latestConflictRate = latestSnapshot?.ConflictRate ?? 0m;
            var latestSuccessfulCrawlRate = latestSnapshot?.SuccessfulCrawlRate ?? source?.Health.SuccessfulCrawlRate ?? 0m;
            var latestChangeActivity = latestSnapshot is null ? 0m : AnalyticsPresentation.GetChangeActivity(latestSnapshot);
            var disagreementRate = disagreements.Length == 0 ? 0m : decimal.Round(disagreements.Average(item => item.DisagreementRate), 1, MidpointRounding.AwayFromZero);
            var hotspotCount = disagreements.Count(item => item.DisagreementRate >= 25m);
            var supportedCategoryCount = source?.SupportedCategoryKeys.Count ?? 0;
            var supportBreadth = decimal.Round(decimal.Divide(supportedCategoryCount * 100m, sourceCount), 1, MidpointRounding.AwayFromZero);
            var trendDelta = latestSnapshot is null || earliestSnapshot is null
                ? 0m
                : decimal.Round(latestSnapshot.HistoricalTrustScore - earliestSnapshot.HistoricalTrustScore, 1, MidpointRounding.AwayFromZero);
            var valueScore = decimal.Clamp(decimal.Round(
                qualityScore * 0.45m
                + latestTrustScore * 0.20m
                + latestCoveragePercent * 0.20m
                + supportBreadth * 0.15m,
                1,
                MidpointRounding.AwayFromZero), 0m, 100m);
            var riskScore = decimal.Clamp(decimal.Round(
                (100m - latestTrustScore) * 0.30m
                + latestChangeActivity * 0.30m
                + disagreementRate * 0.25m
                + (100m - latestCoveragePercent) * 0.15m,
                1,
                MidpointRounding.AwayFromZero), 0m, 100m);

            metrics.Add(new SourceIntelligenceMetricModel(
                sourceName,
                score?.SourceProductCount ?? 0,
                qualityScore,
                score?.CoveragePercent ?? latestCoveragePercent,
                latestAgreementPercent,
                score?.AverageMappedAttributes ?? 0m,
                score?.AverageAttributeConfidence ?? 0m,
                latestTrustScore,
                latestCoveragePercent,
                latestChangeActivity,
                latestConflictRate,
                latestSuccessfulCrawlRate,
                disagreementRate,
                hotspotCount,
                supportedCategoryCount,
                trendDelta,
                valueScore,
                riskScore,
                latestSnapshot is not null));
        }

        return metrics;
    }

    private IReadOnlyList<SupportMatrixRowModel> BuildSupportMatrixRows()
    {
        if (SupportMatrixCategories.Count == 0)
        {
            return [];
        }

        var rows = Sources
            .Where(source => source.IsEnabled || source.SupportedCategoryKeys.Count > 0)
            .OrderByDescending(source => source.SupportedCategoryKeys.Contains(CategoryKey ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            .ThenByDescending(source => SourceMetrics.FirstOrDefault(metric => string.Equals(metric.SourceName, source.DisplayName, StringComparison.OrdinalIgnoreCase))?.ValueScore ?? 0m)
            .ThenBy(source => source.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(source =>
            {
                var metric = SourceMetrics.FirstOrDefault(item => string.Equals(item.SourceName, source.DisplayName, StringComparison.OrdinalIgnoreCase));
                var cells = SupportMatrixCategories.Select(category =>
                {
                    var isSupported = source.SupportedCategoryKeys.Contains(category.CategoryKey, StringComparer.OrdinalIgnoreCase);
                    if (!isSupported)
                    {
                        return new SupportMatrixCellModel(false, category.IsSelected, "-", "neutral");
                    }

                    if (category.IsSelected && metric is not null)
                    {
                        return new SupportMatrixCellModel(true, true, AnalyticsPresentation.FormatPercent(metric.QualityScore), AnalyticsPresentation.GetPositiveTone(metric.QualityScore));
                    }

                    return new SupportMatrixCellModel(true, category.IsSelected, source.Readiness.Status.Equals("ready", StringComparison.OrdinalIgnoreCase) ? "Ready" : "Assigned", source.IsEnabled ? "completed" : "warning");
                }).ToArray();

                return new SupportMatrixRowModel(source.DisplayName, source.Readiness.Status, source.SupportedCategoryKeys.Count, cells);
            })
            .Take(10)
            .ToArray();

        return rows;
    }

    private IReadOnlyList<SourceIntelligenceHighlightModel> BuildTriageHighlights()
    {
        var highestValue = SourceMetrics
            .OrderByDescending(metric => metric.ValueScore)
            .FirstOrDefault();
        var highestRisk = SourceMetrics
            .OrderByDescending(metric => metric.RiskScore)
            .FirstOrDefault();
        var fastestChange = ChangeLeaders.FirstOrDefault();
        var hottestHotspot = DisagreementHotspots.FirstOrDefault();

        return
        [
            new SourceIntelligenceHighlightModel(
                "Highest value",
                highestValue?.SourceName ?? "No source data",
                highestValue is null ? "0%" : AnalyticsPresentation.FormatPercent(highestValue.ValueScore),
                highestValue is null ? "neutral" : AnalyticsPresentation.GetPositiveTone(highestValue.ValueScore),
                highestValue is null ? "No ranked sources are available for this window." : $"Best blend of quality, trust, coverage, and breadth across {TimeRangeLabel.ToLowerInvariant()}"),
            new SourceIntelligenceHighlightModel(
                "Weakest source",
                highestRisk?.SourceName ?? "No source data",
                highestRisk is null ? "0%" : AnalyticsPresentation.FormatPercent(highestRisk.RiskScore),
                highestRisk is null ? "neutral" : AnalyticsPresentation.GetInverseTone(highestRisk.RiskScore),
                highestRisk is null ? "No risk signal is available for this window." : $"Risk blends low trust, low coverage, change activity, and disagreement pressure."),
            new SourceIntelligenceHighlightModel(
                "Fastest change",
                fastestChange?.SourceName ?? "No history",
                fastestChange is null ? "0%" : AnalyticsPresentation.FormatPercent(fastestChange.LatestChangeActivity),
                fastestChange is null ? "neutral" : AnalyticsPresentation.GetInverseTone(fastestChange.LatestChangeActivity),
                fastestChange is null ? "No trust snapshots were recorded in this window." : $"Latest observed change activity with trust drift {GetTrendDeltaText(fastestChange)}."),
            new SourceIntelligenceHighlightModel(
                "Hottest disagreement",
                hottestHotspot?.AttributeKey ?? "No hotspots",
                hottestHotspot is null ? "0%" : AnalyticsPresentation.FormatPercent(hottestHotspot.DisagreementRate),
                hottestHotspot is null ? "neutral" : AnalyticsPresentation.GetInverseTone(hottestHotspot.DisagreementRate),
                hottestHotspot is null ? "No disagreement records were recorded in this window." : $"{hottestHotspot.SourceName} is driving the sharpest disagreement rate in the selected category."),
        ];
    }
}

public sealed record TimeRangeOptionModel(int Days, string Label);

public sealed record SourceIntelligenceMetricModel(
    string SourceName,
    int SourceProductCount,
    decimal QualityScore,
    decimal CoveragePercent,
    decimal AgreementPercent,
    decimal AverageMappedAttributes,
    decimal AverageAttributeConfidence,
    decimal LatestTrustScore,
    decimal LatestCoveragePercent,
    decimal LatestChangeActivity,
    decimal LatestConflictRate,
    decimal LatestSuccessfulCrawlRate,
    decimal LatestDisagreementRate,
    int HotspotCount,
    int SupportedCategoryCount,
    decimal TrendDelta,
    decimal ValueScore,
    decimal RiskScore,
    bool HasHistory);

public sealed record SourceIntelligenceHighlightModel(
    string Label,
    string Name,
    string Value,
    string Tone,
    string Description);

public sealed record SupportMatrixCategoryModel(
    string CategoryKey,
    string DisplayName,
    bool IsSelected);

public sealed record SupportMatrixCellModel(
    bool IsSupported,
    bool IsSelected,
    string Label,
    string Tone);

public sealed record SupportMatrixRowModel(
    string SourceName,
    string ReadinessStatus,
    int SupportedCategoryCount,
    IReadOnlyList<SupportMatrixCellModel> Cells);
