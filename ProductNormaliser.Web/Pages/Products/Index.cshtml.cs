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

    [BindProperty(SupportsGet = true, Name = "minSourceCount")]
    public int? MinSourceCount { get; set; }

    [BindProperty(SupportsGet = true, Name = "freshness")]
    public string? Freshness { get; set; }

    [BindProperty(SupportsGet = true, Name = "conflictStatus")]
    public string? ConflictStatus { get; set; }

    [BindProperty(SupportsGet = true, Name = "completeness")]
    public string? CompletenessStatus { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PageNumber { get; set; } = 1;

    public string? ErrorMessage { get; private set; }

    public IReadOnlyList<CategoryMetadataDto> Categories { get; private set; } = [];

    public ProductListResponseDto Products { get; private set; } = new() { Page = 1, PageSize = 12 };

    public int StaleProducts => Products.Items.Count(product => string.Equals(product.FreshnessStatus, "stale", StringComparison.OrdinalIgnoreCase));

    public int ProductsWithConflicts => Products.Items.Count(product => product.HasConflict);

    public decimal AverageCompleteness => Products.Items.Count == 0
        ? 0m
        : decimal.Round(Products.Items.Average(product => product.CompletenessScore), 2, MidpointRounding.AwayFromZero);

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
            ["minSourceCount"] = MinSourceCount?.ToString(),
            ["freshness"] = Freshness,
            ["conflictStatus"] = ConflictStatus,
            ["completeness"] = CompletenessStatus
        }
    };

    public PageHeroModel Hero => new()
    {
        Eyebrow = "Product Explorer",
        Title = "Search canonical products by quality signals",
        Description = "Use category, source coverage, freshness, conflict, and completeness filters to find products that need attention or products strong enough for downstream analysis.",
        Metrics =
        [
            new HeroMetricModel { Label = "Visible products", Value = Products.Items.Count.ToString() },
            new HeroMetricModel { Label = "Total matches", Value = Products.TotalCount.ToString() },
            new HeroMetricModel { Label = "Stale in view", Value = StaleProducts.ToString() },
            new HeroMetricModel { Label = "Avg completeness", Value = ProductExplorerPresentation.FormatPercent(AverageCompleteness) }
        ]
    };

    public StatusBadgeModel GetFreshnessBadge(ProductSummaryDto product) => ProductExplorerPresentation.GetFreshnessBadge(product.FreshnessStatus, product.FreshnessAgeDays);

    public StatusBadgeModel GetCompletenessBadge(ProductSummaryDto product) => ProductExplorerPresentation.GetCompletenessBadge(product.CompletenessScore, product.CompletenessStatus);

    public StatusBadgeModel GetConflictBadge(ProductSummaryDto product) => ProductExplorerPresentation.GetConflictBadge(product.HasConflict, product.ConflictAttributeCount);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var categoriesTask = adminApiClient.GetCategoriesAsync(cancellationToken);
            var productsTask = adminApiClient.GetProductsAsync(new ProductListQueryDto
            {
                CategoryKey = CategoryKey,
                Search = Search,
                MinSourceCount = MinSourceCount,
                Freshness = Freshness,
                ConflictStatus = ConflictStatus,
                CompletenessStatus = CompletenessStatus,
                Page = Math.Max(1, PageNumber),
                PageSize = 12
            }, cancellationToken);

            await Task.WhenAll(categoriesTask, productsTask);

            Categories = categoriesTask.Result.OrderBy(category => category.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
            Products = productsTask.Result;
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