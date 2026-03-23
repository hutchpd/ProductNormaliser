using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Models;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Pages.Products;

public sealed class DetailsModel(
    IProductNormaliserAdminApiClient adminApiClient,
    ILogger<DetailsModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "productId")]
    public string? ProductId { get; set; }

    [BindProperty(SupportsGet = true, Name = "category")]
    public string? ReturnCategory { get; set; }

    [BindProperty(SupportsGet = true, Name = "search")]
    public string? ReturnSearch { get; set; }

    [BindProperty(SupportsGet = true, Name = "returnPage")]
    public int ReturnPage { get; set; } = 1;

    [BindProperty(SupportsGet = true, Name = "minSourceCount")]
    public int? ReturnMinSourceCount { get; set; }

    [BindProperty(SupportsGet = true, Name = "freshness")]
    public string? ReturnFreshness { get; set; }

    [BindProperty(SupportsGet = true, Name = "conflictStatus")]
    public string? ReturnConflictStatus { get; set; }

    [BindProperty(SupportsGet = true, Name = "completeness")]
    public string? ReturnCompletenessStatus { get; set; }

    [BindProperty(SupportsGet = true, Name = "sort")]
    public string? ReturnSort { get; set; }

    [BindProperty]
    public AnalystNoteInput NoteInput { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public ProductDetailDto? Product { get; private set; }

    public AnalystNoteDto? AnalystNote { get; private set; }

    public IReadOnlyList<ProductChangeEventDto> ProductHistory { get; private set; } = [];

    public IReadOnlyList<ProductSourceComparisonColumnModel> SourceComparisonColumns => Product is null
        ? []
        : ProductInspectionPresentation.GetSourceComparisonColumns(Product);

    public IReadOnlyList<ProductSourceComparisonRowModel> SourceComparisonRows => Product is null
        ? []
        : ProductInspectionPresentation.GetSourceComparisonRows(Product);

    public IReadOnlyList<ProductEvidenceInspectorRowModel> EvidenceInspectorRows => Product is null
        ? []
        : ProductInspectionPresentation.GetEvidenceInspectorRows(Product);

    public IReadOnlyList<ProductConflictPanelRowModel> ConflictRows => Product is null
        ? []
        : ProductInspectionPresentation.GetConflictRows(Product);

    public ProductInvestigationSummaryModel InvestigationSummary => Product is null
        ? new ProductInvestigationSummaryModel()
        : ProductInspectionPresentation.GetInvestigationSummary(Product, ProductHistory);

    public IReadOnlyList<ProductSourceDriftIndicatorModel> SourceDriftIndicators => Product is null
        ? []
        : ProductInspectionPresentation.GetSourceDriftIndicators(Product, ProductHistory);

    public IReadOnlyList<ProductCanonicalExplanationModel> CanonicalExplanations => Product is null
        ? []
        : ProductInspectionPresentation.GetCanonicalExplanations(Product, ProductHistory);

    public IReadOnlyList<ProductHistoryTimelineEntryModel> Timeline => Product is null
        ? []
        : ProductInspectionPresentation.GetHistoryTimeline(Product, ProductHistory);

    public IReadOnlyList<ProductAttributeHistoryGroupModel> AttributeHistory => Product is null
        ? []
        : ProductInspectionPresentation.GetAttributeHistory(Product, ProductHistory);

    public StatusBadgeModel FreshnessBadge => Product is null
        ? new StatusBadgeModel { Text = "Unknown freshness", Tone = "neutral" }
        : ProductExplorerPresentation.GetFreshnessBadge(Product.FreshnessStatus, Product.FreshnessAgeDays);

    public StatusBadgeModel CompletenessBadge => Product is null
        ? new StatusBadgeModel { Text = "Unknown completeness", Tone = "neutral" }
        : ProductExplorerPresentation.GetCompletenessBadge(Product.CompletenessScore, Product.CompletenessStatus);

    public StatusBadgeModel ConflictBadge => Product is null
        ? new StatusBadgeModel { Text = "No conflict data", Tone = "neutral" }
        : ProductExplorerPresentation.GetConflictBadge(Product.HasConflict, Product.ConflictAttributeCount);

    public PageHeroModel Hero => Product is null
        ? new PageHeroModel
        {
            Eyebrow = "Product Detail",
            Title = "Product not found",
            Description = "The requested canonical product could not be loaded.",
            Metrics = []
        }
        : new PageHeroModel
        {
            Eyebrow = "Product Detail",
            Title = Product.DisplayName,
            Description = "Review the canonical summary, value explanations, source drift, raw evidence, disagreements, and change history in one place.",
            Metrics =
            [
                new HeroMetricModel { Label = "Sources", Value = Product.SourceCount.ToString() },
                new HeroMetricModel { Label = "Evidence", Value = Product.EvidenceCount.ToString() },
                new HeroMetricModel { Label = "Freshness", Value = FreshnessBadge.Text },
                new HeroMetricModel { Label = "Completeness", Value = ProductExplorerPresentation.FormatPercent(Product.CompletenessScore) }
            ]
        };

    public Dictionary<string, string?> BackToExplorerRouteValues => new(StringComparer.OrdinalIgnoreCase)
    {
        ["category"] = ReturnCategory,
        ["search"] = ReturnSearch,
        ["page"] = Math.Max(1, ReturnPage).ToString(),
        ["minSourceCount"] = ReturnMinSourceCount?.ToString(),
        ["freshness"] = ReturnFreshness,
        ["conflictStatus"] = ReturnConflictStatus,
        ["completeness"] = ReturnCompletenessStatus,
        ["sort"] = ReturnSort
    };

    public string BackToExplorerUrl => QueryHelpers.AddQueryString("/Products", BackToExplorerRouteValues);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ProductId))
        {
            ErrorMessage = "A product id is required.";
            return;
        }

        try
        {
            var productTask = adminApiClient.GetProductAsync(ProductId, cancellationToken);
            var historyTask = adminApiClient.GetProductHistoryAsync(ProductId, cancellationToken);
            var noteTask = adminApiClient.GetAnalystNoteAsync("product", ProductId, cancellationToken);
            await Task.WhenAll(productTask, historyTask, noteTask);

            Product = productTask.Result;
            ProductHistory = historyTask.Result;
            AnalystNote = noteTask.Result;

            if (AnalystNote is not null && string.IsNullOrWhiteSpace(NoteInput.Content))
            {
                NoteInput = new AnalystNoteInput
                {
                    Title = AnalystNote.Title,
                    Content = AnalystNote.Content
                };
            }

            if (Product is null)
            {
                ErrorMessage = $"Product '{ProductId}' was not found.";
            }
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to load product detail page data for {ProductId}.", ProductId);
            ErrorMessage = exception.Message;
        }
    }

    public async Task<IActionResult> OnPostSaveNoteAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ProductId))
        {
            ErrorMessage = "A product id is required.";
            return Page();
        }

        try
        {
            await adminApiClient.SaveAnalystNoteAsync(new UpsertAnalystNoteRequest
            {
                TargetType = "product",
                TargetId = ProductId,
                Title = NoteInput.Title,
                Content = NoteInput.Content
            }, cancellationToken);
            StatusMessage = "Saved product note.";
            return RedirectToPage(new
            {
                productId = ProductId,
                category = ReturnCategory,
                search = ReturnSearch,
                returnPage = ReturnPage,
                minSourceCount = ReturnMinSourceCount,
                freshness = ReturnFreshness,
                conflictStatus = ReturnConflictStatus,
                completeness = ReturnCompletenessStatus,
                sort = ReturnSort
            });
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to save product note for {ProductId}.", ProductId);
            ErrorMessage = exception.Message;
            await OnGetAsync(cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteNoteAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ProductId))
        {
            ErrorMessage = "A product id is required.";
            return Page();
        }

        try
        {
            await adminApiClient.DeleteAnalystNoteAsync("product", ProductId, cancellationToken);
            StatusMessage = "Deleted product note.";
            return RedirectToPage(new
            {
                productId = ProductId,
                category = ReturnCategory,
                search = ReturnSearch,
                returnPage = ReturnPage,
                minSourceCount = ReturnMinSourceCount,
                freshness = ReturnFreshness,
                conflictStatus = ReturnConflictStatus,
                completeness = ReturnCompletenessStatus,
                sort = ReturnSort
            });
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to delete product note for {ProductId}.", ProductId);
            ErrorMessage = exception.Message;
            await OnGetAsync(cancellationToken);
            return Page();
        }
    }

    public sealed class AnalystNoteInput
    {
        [StringLength(120)]
        public string? Title { get; set; }

        [StringLength(4000)]
        public string Content { get; set; } = string.Empty;
    }
}