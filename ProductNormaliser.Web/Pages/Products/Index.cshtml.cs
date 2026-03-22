using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Models;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Pages.Products;

public sealed class IndexModel(
    IProductNormaliserAdminApiClient adminApiClient,
    ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "category")]
    public string? CategoryKey { get; set; }

    [BindProperty(SupportsGet = true, Name = "search")]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true, Name = "productId")]
    public string? SelectedProductId { get; set; }

    public string? ErrorMessage { get; private set; }

    public IReadOnlyList<CategoryMetadataDto> Categories { get; private set; } = [];

    public ProductListResponseDto Products { get; private set; } = new() { Page = 1, PageSize = 12 };

    public ProductDetailDto? SelectedProduct { get; private set; }

    public IReadOnlyList<ProductChangeEventDto> ProductHistory { get; private set; } = [];

    public IReadOnlyList<ProductSourceComparisonColumnModel> SourceComparisonColumns => SelectedProduct is null
        ? []
        : ProductInspectionPresentation.GetSourceComparisonColumns(SelectedProduct);

    public IReadOnlyList<ProductSourceComparisonRowModel> SourceComparisonRows => SelectedProduct is null
        ? []
        : ProductInspectionPresentation.GetSourceComparisonRows(SelectedProduct);

    public IReadOnlyList<ProductEvidenceInspectorRowModel> EvidenceInspectorRows => SelectedProduct is null
        ? []
        : ProductInspectionPresentation.GetEvidenceInspectorRows(SelectedProduct);

    public IReadOnlyList<ProductConflictPanelRowModel> ConflictRows => SelectedProduct is null
        ? []
        : ProductInspectionPresentation.GetConflictRows(SelectedProduct);

    public IReadOnlyList<ProductHistoryTimelineEntryModel> Timeline => ProductInspectionPresentation.GetHistoryTimeline(ProductHistory);

    public PaginationModel Pagination => new()
    {
        PagePath = "/Products/Index",
        CurrentPage = Products.Page,
        TotalPages = Products.TotalPages,
        TotalCount = Products.TotalCount,
        RouteValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["category"] = CategoryKey,
            ["search"] = Search,
            ["productId"] = SelectedProductId
        }
    };

    public PageHeroModel Hero => new()
    {
        Eyebrow = "Product Catalogue",
        Title = "Browse canonical products and inspect merge history",
        Description = "This is a lightweight internal catalogue rather than a full consumer search experience: paged product summaries on the left, detailed merged evidence and change history on demand.",
        Metrics =
        [
            new HeroMetricModel { Label = "Visible products", Value = Products.Items.Count.ToString() },
            new HeroMetricModel { Label = "Total matches", Value = Products.TotalCount.ToString() },
            new HeroMetricModel { Label = "History events", Value = ProductHistory.Count.ToString() }
        ]
    };

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var categoriesTask = adminApiClient.GetCategoriesAsync(cancellationToken);
            var productsTask = adminApiClient.GetProductsAsync(new ProductListQueryDto
            {
                CategoryKey = CategoryKey,
                Search = Search,
                Page = Math.Max(1, PageNumber),
                PageSize = 12
            }, cancellationToken);

            await Task.WhenAll(categoriesTask, productsTask);

            Categories = categoriesTask.Result.OrderBy(category => category.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
            Products = productsTask.Result;

            if (!string.IsNullOrWhiteSpace(SelectedProductId))
            {
                var productTask = adminApiClient.GetProductAsync(SelectedProductId, cancellationToken);
                var historyTask = adminApiClient.GetProductHistoryAsync(SelectedProductId, cancellationToken);
                await Task.WhenAll(productTask, historyTask);

                SelectedProduct = productTask.Result;
                ProductHistory = historyTask.Result;

                if (SelectedProduct is null)
                {
                    ErrorMessage = $"Product '{SelectedProductId}' was not found.";
                }
            }
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to load products page data.");
            ErrorMessage = exception.Message;
            Categories = [];
            Products = new ProductListResponseDto { Page = 1, PageSize = 12 };
        }
    }
}