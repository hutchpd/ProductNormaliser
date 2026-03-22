using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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

    [BindProperty(SupportsGet = true, Name = "page")]
    public int ReturnPage { get; set; } = 1;

    [BindProperty(SupportsGet = true, Name = "minSourceCount")]
    public int? ReturnMinSourceCount { get; set; }

    [BindProperty(SupportsGet = true, Name = "freshness")]
    public string? ReturnFreshness { get; set; }

    [BindProperty(SupportsGet = true, Name = "conflictStatus")]
    public string? ReturnConflictStatus { get; set; }

    [BindProperty(SupportsGet = true, Name = "completeness")]
    public string? ReturnCompletenessStatus { get; set; }

    public string? ErrorMessage { get; private set; }

    public ProductDetailDto? Product { get; private set; }

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

    public IReadOnlyList<ProductHistoryTimelineEntryModel> Timeline => ProductInspectionPresentation.GetHistoryTimeline(ProductHistory);

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
            Description = "Review the canonical summary, key attributes, source comparison, raw evidence, disagreements, and change history in one place.",
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
        ["completeness"] = ReturnCompletenessStatus
    };

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
            await Task.WhenAll(productTask, historyTask);

            Product = productTask.Result;
            ProductHistory = historyTask.Result;

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
}