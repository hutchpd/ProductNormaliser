using Microsoft.Extensions.Logging.Abstractions;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Models;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Tests;

public sealed class ProductExplorerPageTests
{
    [Test]
    public async Task ProductsIndex_OnGetAsync_ForwardsFilterStateToApi()
    {
        var client = new FakeAdminApiClient
        {
            Categories = [new CategoryMetadataDto { CategoryKey = "tv", DisplayName = "TVs", IsEnabled = true }],
            ProductPage = new ProductListResponseDto { Page = 2, PageSize = 12, TotalCount = 0, TotalPages = 0 }
        };

        var model = new ProductNormaliser.Web.Pages.Products.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Products.IndexModel>.Instance)
        {
            CategoryKey = "tv",
            Search = "sony",
            MinSourceCount = 3,
            Freshness = "stale",
            ConflictStatus = "with_conflicts",
            CompletenessStatus = "partial",
            PageNumber = 2
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(client.LastProductQuery, Is.Not.Null);
            Assert.That(client.LastProductQuery!.CategoryKey, Is.EqualTo("tv"));
            Assert.That(client.LastProductQuery.Search, Is.EqualTo("sony"));
            Assert.That(client.LastProductQuery.MinSourceCount, Is.EqualTo(3));
            Assert.That(client.LastProductQuery.Freshness, Is.EqualTo("stale"));
            Assert.That(client.LastProductQuery.ConflictStatus, Is.EqualTo("with_conflicts"));
            Assert.That(client.LastProductQuery.CompletenessStatus, Is.EqualTo("partial"));
            Assert.That(model.Pagination.RouteValues["search"], Is.EqualTo("sony"));
        });
    }

    [Test]
    public async Task ProductsIndex_OnGetAsync_LoadsSearchResultsForAnalysisView()
    {
        var client = new FakeAdminApiClient
        {
            Categories = [new CategoryMetadataDto { CategoryKey = "tv", DisplayName = "TVs", IsEnabled = true }],
            ProductPage = new ProductListResponseDto
            {
                Items =
                [
                    new ProductSummaryDto
                    {
                        Id = "canon-1",
                        CategoryKey = "tv",
                        Brand = "Sony",
                        ModelNumber = "XR-55A80L",
                        DisplayName = "Sony Bravia XR",
                        SourceCount = 4,
                        EvidenceCount = 14,
                        AttributeCount = 10,
                        HasConflict = true,
                        ConflictAttributeCount = 2,
                        CompletenessScore = 0.75m,
                        CompletenessStatus = "partial",
                        FreshnessStatus = "stale",
                        FreshnessAgeDays = 42,
                        KeyAttributes = [new ProductKeyAttributeDto { AttributeKey = "panel_type", DisplayName = "Panel Type", Value = "OLED" }],
                        UpdatedUtc = new DateTime(2026, 03, 20, 10, 10, 00, DateTimeKind.Utc)
                    }
                ],
                Page = 1,
                PageSize = 12,
                TotalCount = 1,
                TotalPages = 1
            }
        };

        var model = new ProductNormaliser.Web.Pages.Products.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Products.IndexModel>.Instance)
        {
            Search = "bravia"
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(client.LastProductQuery!.Search, Is.EqualTo("bravia"));
            Assert.That(model.Products.Items, Has.Count.EqualTo(1));
            Assert.That(model.ProductsWithConflicts, Is.EqualTo(1));
            Assert.That(model.StaleProducts, Is.EqualTo(1));
            Assert.That(model.AverageCompleteness, Is.EqualTo(0.75m));
        });
    }

    [Test]
    public async Task ProductDetails_OnGetAsync_ReturnsMissingProductErrorState()
    {
        var client = new FakeAdminApiClient();
        var model = new ProductNormaliser.Web.Pages.Products.DetailsModel(client, NullLogger<ProductNormaliser.Web.Pages.Products.DetailsModel>.Instance)
        {
            ProductId = "missing"
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(model.Product, Is.Null);
            Assert.That(model.ErrorMessage, Is.EqualTo("Product 'missing' was not found."));
        });
    }

    [Test]
    public async Task ProductDetails_OnGetAsync_ShowsApiErrorState()
    {
        var client = new FakeThrowingProductClient();
        var model = new ProductNormaliser.Web.Pages.Products.DetailsModel(client, NullLogger<ProductNormaliser.Web.Pages.Products.DetailsModel>.Instance)
        {
            ProductId = "canon-1"
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.That(model.ErrorMessage, Is.EqualTo("Admin API is unavailable."));
    }

    [Test]
    public void ProductExplorerPresentation_ReturnsRenderingBadges()
    {
        var freshness = ProductExplorerPresentation.GetFreshnessBadge("stale", 42);
        var completeness = ProductExplorerPresentation.GetCompletenessBadge(0.42m, "sparse");
        var conflict = ProductExplorerPresentation.GetConflictBadge(true, 3);

        Assert.Multiple(() =>
        {
            Assert.That(freshness.Text, Does.Contain("42d"));
            Assert.That(freshness.Tone, Is.EqualTo("danger"));
            Assert.That(completeness.Text, Is.EqualTo("Sparse 42%"));
            Assert.That(conflict.Text, Is.EqualTo("3 conflicts"));
        });
    }

    private sealed class FakeThrowingProductClient : IProductNormaliserAdminApiClient
    {
        public Task<StatsDto> GetStatsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<CategoryMetadataDto>> GetCategoriesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<CategoryFamilyDto>> GetCategoryFamiliesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<CategoryMetadataDto>> GetEnabledCategoriesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CategoryDetailDto?> GetCategoryDetailAsync(string categoryKey, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SourceDto>> GetSourcesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SourceDto?> GetSourceAsync(string sourceId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SourceDto> RegisterSourceAsync(RegisterSourceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SourceDto> UpdateSourceAsync(string sourceId, UpdateSourceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SourceDto> EnableSourceAsync(string sourceId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SourceDto> DisableSourceAsync(string sourceId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SourceDto> AssignCategoriesAsync(string sourceId, AssignSourceCategoriesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SourceDto> UpdateThrottlingAsync(string sourceId, UpdateSourceThrottlingRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CrawlJobListResponseDto> GetCrawlJobsAsync(CrawlJobQueryDto? query = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CrawlJobDto?> GetCrawlJobAsync(string jobId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CrawlJobDto> CreateCrawlJobAsync(CreateCrawlJobRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CrawlJobDto> CancelCrawlJobAsync(string jobId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ProductListResponseDto> GetProductsAsync(ProductListQueryDto? query = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ProductDetailDto?> GetProductAsync(string productId, CancellationToken cancellationToken = default) => Task.FromException<ProductDetailDto?>(new AdminApiException("Admin API is unavailable."));
        public Task<IReadOnlyList<ProductChangeEventDto>> GetProductHistoryAsync(string productId, CancellationToken cancellationToken = default) => Task.FromException<IReadOnlyList<ProductChangeEventDto>>(new AdminApiException("Admin API is unavailable."));
    }
}