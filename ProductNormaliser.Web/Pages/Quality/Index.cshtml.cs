using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Globalization;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Models;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Pages.Quality;

public sealed class IndexModel(
    IProductNormaliserAdminApiClient adminApiClient,
    ILogger<IndexModel> logger) : PageModel
{
    private const string RoutePath = "/Quality/Index";

    [BindProperty(SupportsGet = true, Name = "category")]
    public string? CategoryKey { get; set; }

    [BindProperty(SupportsGet = true, Name = "view")]
    public string? SavedViewId { get; set; }

    [BindProperty]
    public string SaveViewName { get; set; } = string.Empty;

    [BindProperty]
    public string? SaveViewDescription { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public string? WorkflowMessage { get; private set; }

    public IReadOnlyList<CategoryMetadataDto> Categories { get; private set; } = [];

    public IReadOnlyList<SavedAnalystWorkflowCardModel> SavedWorkflows { get; private set; } = [];

    public CategoryMetadataDto? SelectedCategory => Categories.FirstOrDefault(category => string.Equals(category.CategoryKey, CategoryKey, StringComparison.OrdinalIgnoreCase));

    public DetailedCoverageResponseDto Coverage { get; private set; } = new();

    public IReadOnlyList<UnmappedAttributeDto> UnmappedAttributes { get; private set; } = [];

    public IReadOnlyList<SourceAttributeDisagreementDto> SourceDisagreements { get; private set; } = [];

    public IReadOnlyList<AttributeStabilityDto> AttributeStability { get; private set; } = [];

    public bool IsAwaitingSelection => string.IsNullOrWhiteSpace(CategoryKey);

    public bool IsEmpty => !IsAwaitingSelection
        && Coverage.Attributes.Count == 0
        && UnmappedAttributes.Count == 0
        && SourceDisagreements.Count == 0
        && AttributeStability.Count == 0;

    public IReadOnlyList<AttributeCoverageDetailDto> CoverageHeatmap => Coverage.Attributes
        .OrderBy(attribute => attribute.CoveragePercent)
        .ThenByDescending(attribute => attribute.ConflictPercent)
        .ThenBy(attribute => attribute.DisplayName, StringComparer.OrdinalIgnoreCase)
        .Take(12)
        .ToArray();

    public DisagreementMatrixModel DisagreementMatrix => AnalyticsPresentation.BuildDisagreementMatrix(SourceDisagreements);

    public IReadOnlyList<AttributeStabilityDto> StabilityChart => AttributeStability
        .OrderByDescending(attribute => attribute.IsSuspicious)
        .ThenBy(attribute => attribute.StabilityScore)
        .ThenByDescending(attribute => attribute.ChangeCount)
        .ThenBy(attribute => attribute.AttributeKey, StringComparer.OrdinalIgnoreCase)
        .Take(12)
        .ToArray();

    public decimal AverageCoverage => Coverage.Attributes.Count == 0
        ? 0m
        : decimal.Round(Coverage.Attributes.Average(attribute => attribute.CoveragePercent), 2, MidpointRounding.AwayFromZero);

    public decimal AverageReliability => Coverage.Attributes.Count == 0
        ? 0m
        : decimal.Round(Coverage.Attributes.Average(attribute => attribute.ReliabilityScore), 2, MidpointRounding.AwayFromZero);

    public int SuspiciousAttributeCount => AttributeStability.Count(attribute => attribute.IsSuspicious);

    public PageHeroModel Hero => new()
    {
        Eyebrow = "Quality Dashboard",
        Title = SelectedCategory is null ? "Category-aware quality intelligence" : $"{SelectedCategory.DisplayName} quality dashboard",
        Description = SelectedCategory is null
            ? "Select a category to inspect coverage heatmaps, mapping backlog, disagreement hotspots, and attribute stability without assuming any TV-specific schema."
            : "Review coverage, backlog, disagreement, and stability signals for the selected category. Every panel is driven by the chosen category schema and evidence patterns.",
        Metrics =
        [
            new HeroMetricModel { Label = "Categories", Value = Categories.Count.ToString() },
            new HeroMetricModel { Label = "Tracked attributes", Value = Coverage.Attributes.Count.ToString() },
            new HeroMetricModel { Label = "Avg coverage", Value = AnalyticsPresentation.FormatPercent(AverageCoverage) },
            new HeroMetricModel { Label = "Suspicious", Value = SuspiciousAttributeCount.ToString() }
        ]
    };

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var categoriesTask = adminApiClient.GetCategoriesAsync(cancellationToken);
            var workflowsTask = adminApiClient.GetAnalystWorkflowsAsync(routePath: RoutePath, cancellationToken: cancellationToken);
            await Task.WhenAll(categoriesTask, workflowsTask);

            Categories = InteractiveCategoryFilter.Apply(categoriesTask.Result);
            var workflowDefinitions = workflowsTask.Result;
            var restoredWorkflow = await ResolveSavedWorkflowAsync(workflowDefinitions, cancellationToken);
            ApplySavedWorkflow(restoredWorkflow);
            var categoryContext = CategoryContextStateFactory.Resolve(
                Categories,
                CategoryKey,
                null,
                PageContext?.HttpContext?.Request.Cookies[CategoryContextState.CookieName]);
            if (restoredWorkflow is not null && !string.IsNullOrWhiteSpace(restoredWorkflow.PrimaryCategoryKey) && !Categories.Any(category => string.Equals(category.CategoryKey, restoredWorkflow.PrimaryCategoryKey, StringComparison.OrdinalIgnoreCase)))
            {
                WorkflowMessage = $"Saved view '{restoredWorkflow.Name}' referenced missing category '{restoredWorkflow.PrimaryCategoryKey}'. Showing the default active category instead.";
            }

            CategoryKey = categoryContext.PrimaryCategoryKey;
            SavedWorkflows = AnalystWorkspacePresentation.BuildWorkflowCards(
                workflowDefinitions,
                Categories,
                SavedViewId,
                AnalystWorkspacePresentation.WorkflowTypeConflictReviewQueue,
                AnalystWorkspacePresentation.WorkflowTypeSelectedCategories);

            if (IsAwaitingSelection)
            {
                return;
            }

            var coverageTask = adminApiClient.GetDetailedCoverageAsync(CategoryKey!, cancellationToken);
            var unmappedTask = adminApiClient.GetUnmappedAttributesAsync(CategoryKey!, cancellationToken);
            var disagreementsTask = adminApiClient.GetSourceDisagreementsAsync(CategoryKey!, cancellationToken: cancellationToken);
            var stabilityTask = adminApiClient.GetAttributeStabilityAsync(CategoryKey!, cancellationToken);
            await Task.WhenAll(coverageTask, unmappedTask, disagreementsTask, stabilityTask);

            Coverage = coverageTask.Result;
            UnmappedAttributes = unmappedTask.Result
                .OrderByDescending(attribute => attribute.OccurrenceCount)
                .ThenByDescending(attribute => attribute.LastSeenUtc)
                .Take(12)
                .ToArray();
            SourceDisagreements = disagreementsTask.Result;
            AttributeStability = stabilityTask.Result;
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to load category quality dashboard for {CategoryKey}.", CategoryKey);
            ErrorMessage = exception.Message;
            Coverage = new DetailedCoverageResponseDto();
            UnmappedAttributes = [];
            SourceDisagreements = [];
            AttributeStability = [];
        }
    }

    public async Task<IActionResult> OnPostSaveViewAsync(string workflowType, CancellationToken cancellationToken)
    {
        try
        {
            var workflow = await adminApiClient.SaveAnalystWorkflowAsync(new UpsertAnalystWorkflowRequest
            {
                Id = SavedViewId,
                Name = string.IsNullOrWhiteSpace(SaveViewName) ? $"{(string.IsNullOrWhiteSpace(CategoryKey) ? "Category" : CategoryKey)} quality queue" : SaveViewName.Trim(),
                Description = string.IsNullOrWhiteSpace(SaveViewDescription) ? null : SaveViewDescription.Trim(),
                WorkflowType = workflowType,
                RoutePath = RoutePath,
                PrimaryCategoryKey = CategoryKey,
                SelectedCategoryKeys = string.IsNullOrWhiteSpace(CategoryKey) ? [] : [CategoryKey],
                State = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["category"] = CategoryKey ?? string.Empty
                }
            }, cancellationToken);
            StatusMessage = $"Saved analyst view '{workflow.Name}'.";
            return RedirectToPage(new { category = CategoryKey, view = workflow.Id });
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to save quality workflow for {CategoryKey}.", CategoryKey);
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
            return RedirectToPage(new { category = CategoryKey });
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to delete quality workflow {WorkflowId}.", workflowId);
            ErrorMessage = exception.Message;
            await OnGetAsync(cancellationToken);
            return Page();
        }
    }

    public async Task<FileContentResult> OnGetExportSummaryAsync(CancellationToken cancellationToken)
    {
        var rows = new List<IReadOnlyList<string>>
        {
            new string[] { "Section", "Key", "Label", "Metric", "Value", "Context" }
        };

        if (!string.IsNullOrWhiteSpace(CategoryKey))
        {
            var coverageTask = adminApiClient.GetDetailedCoverageAsync(CategoryKey!, cancellationToken);
            var unmappedTask = adminApiClient.GetUnmappedAttributesAsync(CategoryKey!, cancellationToken);
            var disagreementsTask = adminApiClient.GetSourceDisagreementsAsync(CategoryKey!, cancellationToken: cancellationToken);
            var stabilityTask = adminApiClient.GetAttributeStabilityAsync(CategoryKey!, cancellationToken);
            await Task.WhenAll(coverageTask, unmappedTask, disagreementsTask, stabilityTask);

            var coverage = coverageTask.Result;
            var unmapped = unmappedTask.Result;
            var disagreements = disagreementsTask.Result;
            var stability = stabilityTask.Result;

            rows.Add(new string[] { "summary", coverage.CategoryKey, "Total canonical products", "count", coverage.TotalCanonicalProducts.ToString(CultureInfo.InvariantCulture), string.Empty });
            rows.Add(new string[] { "summary", coverage.CategoryKey, "Total source products", "count", coverage.TotalSourceProducts.ToString(CultureInfo.InvariantCulture), string.Empty });

            rows.AddRange(coverage.Attributes.Select(attribute => (IReadOnlyList<string>)
            new string[]
            {
                "coverage",
                attribute.AttributeKey,
                attribute.DisplayName,
                "coverage_percent",
                attribute.CoveragePercent.ToString(CultureInfo.InvariantCulture),
                $"reliability={attribute.ReliabilityScore.ToString(CultureInfo.InvariantCulture)};conflict={attribute.ConflictPercent.ToString(CultureInfo.InvariantCulture)}"
            }));

            rows.AddRange(unmapped.Select(item => (IReadOnlyList<string>)
            new string[]
            {
                "unmapped",
                item.RawAttributeKey,
                item.CanonicalKey,
                "occurrence_count",
                item.OccurrenceCount.ToString(CultureInfo.InvariantCulture),
                string.Join(" | ", item.SourceNames)
            }));

            rows.AddRange(disagreements.Select(item => (IReadOnlyList<string>)
            new string[]
            {
                "disagreement",
                item.AttributeKey,
                item.SourceName,
                "disagreement_rate",
                item.DisagreementRate.ToString(CultureInfo.InvariantCulture),
                $"comparisons={item.TotalComparisons.ToString(CultureInfo.InvariantCulture)}"
            }));

            rows.AddRange(stability.Select(item => (IReadOnlyList<string>)
            new string[]
            {
                "stability",
                item.AttributeKey,
                item.AttributeKey,
                "stability_score",
                item.StabilityScore.ToString(CultureInfo.InvariantCulture),
                item.SuspicionReason ?? string.Empty
            }));
        }

        var categorySegment = string.IsNullOrWhiteSpace(CategoryKey) ? "all-categories" : CategoryKey.Trim().ToLowerInvariant();
        return File(CsvExportBuilder.Build(rows), "text/csv", $"quality-summary-{categorySegment}.csv");
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
    }
}
