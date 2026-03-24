using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Models;

namespace ProductNormaliser.Web.Tests;

[TestFixture]
public sealed class CategoryContextWorkflowTests
{
    [Test]
    public void CategoryContextStateFactory_UsesPersistedSelectionAsPrimaryFallback()
    {
        var state = CategoryContextStateFactory.Resolve(CreateCategories(), null, null, "tv,monitor");

        Assert.Multiple(() =>
        {
            Assert.That(state.PrimaryCategoryKey, Is.EqualTo("tv"));
            Assert.That(state.SelectedCategoryKeys, Is.EqualTo(new[] { "tv", "monitor" }));
            Assert.That(state.UsedPersistedSelection, Is.True);
        });
    }

    [Test]
    public void CategoryContextStateFactory_IgnoresInvalidRequestedCategoriesAndFallsBackToPersistedSelection()
    {
        var state = CategoryContextStateFactory.Resolve(CreateCategories(), "unknown", ["unknown", "monitor"], "tv");

        Assert.Multiple(() =>
        {
            Assert.That(state.PrimaryCategoryKey, Is.EqualTo("monitor"));
            Assert.That(state.SelectedCategoryKeys, Is.EqualTo(new[] { "monitor" }));
            Assert.That(state.InvalidCategoryKeys, Does.Contain("unknown"));
        });
    }

    [Test]
    public async Task ProductsIndex_OnGetAsync_UsesPersistedPrimaryCategoryWhenQueryCategoryIsInvalid()
    {
        var client = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            ProductPage = new ProductListResponseDto { Page = 1, PageSize = 12 }
        };

        var model = new ProductNormaliser.Web.Pages.Products.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Products.IndexModel>.Instance)
        {
            CategoryKey = "unknown",
            PageContext = CreatePageContext("monitor")
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(model.CategoryKey, Is.EqualTo("monitor"));
            Assert.That(client.LastProductQuery, Is.Not.Null);
            Assert.That(client.LastProductQuery!.CategoryKey, Is.EqualTo("monitor"));
        });
    }

    [Test]
    public async Task QualityIndex_OnGetAsync_UsesPersistedPrimaryCategoryWhenQueryIsMissing()
    {
        var client = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            DetailedCoverage = new DetailedCoverageResponseDto { CategoryKey = "tv" }
        };

        var model = new ProductNormaliser.Web.Pages.Quality.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Quality.IndexModel>.Instance)
        {
            PageContext = CreatePageContext("tv")
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(model.CategoryKey, Is.EqualTo("tv"));
            Assert.That(client.LastCoverageCategoryKey, Is.EqualTo("tv"));
        });
    }

    [Test]
    public async Task CategoryContext_PersistsAcrossPagesInWebWorkflow()
    {
        var fakeAdminApiClient = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            DetailedCoverage = new DetailedCoverageResponseDto { CategoryKey = "tv" },
            ProductPage = new ProductListResponseDto { Page = 1, PageSize = 12 }
        };

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var seedResponse = await client.GetAsync("/Categories/Index?selectedCategory=tv&selectedCategory=monitor");
        seedResponse.EnsureSuccessStatusCode();

        var qualityHtml = await client.GetStringAsync("/Quality/Index");
        var productsHtml = await client.GetStringAsync("/Products/Index");

        Assert.Multiple(() =>
        {
            Assert.That(fakeAdminApiClient.LastCoverageCategoryKey, Is.EqualTo("tv"));
            Assert.That(fakeAdminApiClient.LastProductQuery, Is.Not.Null);
            Assert.That(fakeAdminApiClient.LastProductQuery!.CategoryKey, Is.EqualTo("tv"));
            Assert.That(qualityHtml, Does.Contain("Current Category Context"));
            Assert.That(productsHtml, Does.Contain("TVs &#x2B;1 more"));
        });
    }

    [Test]
    public async Task CrawlJobsPage_RendersPersistedMultiCategorySelectionFromShellContext()
    {
        var fakeAdminApiClient = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            Sources =
            [
                new SourceDto
                {
                    SourceId = "ao_uk",
                    DisplayName = "AO UK",
                    IsEnabled = true,
                    SupportedCategoryKeys = ["tv", "monitor"]
                }
            ]
        };

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var seedResponse = await client.GetAsync("/Categories/Index?selectedCategory=tv&selectedCategory=monitor");
        seedResponse.EnsureSuccessStatusCode();

        var crawlHtml = await client.GetStringAsync("/CrawlJobs/Index");

        Assert.Multiple(() =>
        {
            Assert.That(crawlHtml, Does.Contain("TVs &#x2B;1 more"));
            Assert.That(crawlHtml, Does.Contain("data-selected-count>2<"));
            Assert.That(crawlHtml, Does.Contain("data-selected-chip>TVs<"));
            Assert.That(crawlHtml, Does.Contain("data-selected-chip>Monitors<"));
        });
    }

    [Test]
    public async Task CrawlJobsPage_WhenNoSourcesExist_ShowsSourceRegistryCallToAction()
    {
        var fakeAdminApiClient = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            Sources = []
        };

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var seedResponse = await client.GetAsync("/Categories/Index?selectedCategory=tv");
        seedResponse.EnsureSuccessStatusCode();

        var crawlHtml = await client.GetStringAsync("/CrawlJobs/Index");

        Assert.Multiple(() =>
        {
            Assert.That(crawlHtml, Does.Contain("No crawl sources are registered yet."));
            Assert.That(crawlHtml, Does.Contain("Open source registry"));
            Assert.That(crawlHtml, Does.Contain("/Sources?category=tv"));
        });
    }

    private static PageContext CreatePageContext(string persistedSelection)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = $"{CategoryContextState.CookieName}={persistedSelection}";
        return new PageContext { HttpContext = httpContext };
    }

    private static IReadOnlyList<CategoryMetadataDto> CreateCategories()
    {
        return
        [
            new CategoryMetadataDto
            {
                CategoryKey = "tv",
                DisplayName = "TVs",
                FamilyKey = "display",
                FamilyDisplayName = "Display",
                IsEnabled = true,
                CrawlSupportStatus = "Supported",
                SchemaCompletenessScore = 0.95m
            },
            new CategoryMetadataDto
            {
                CategoryKey = "monitor",
                DisplayName = "Monitors",
                FamilyKey = "display",
                FamilyDisplayName = "Display",
                IsEnabled = true,
                CrawlSupportStatus = "Supported",
                SchemaCompletenessScore = 0.93m
            },
            new CategoryMetadataDto
            {
                CategoryKey = "laptop",
                DisplayName = "Laptops",
                FamilyKey = "computing",
                FamilyDisplayName = "Computing",
                IsEnabled = true,
                CrawlSupportStatus = "Supported",
                SchemaCompletenessScore = 0.91m
            }
        ];
    }
}