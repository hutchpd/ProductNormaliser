using Microsoft.Extensions.Logging.Abstractions;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Pages;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Tests;

[TestFixture]
public sealed class OperatorLandingPageTests
{
    [Test]
    public void OperatorLanding_DefaultsToLoadingState()
    {
        var model = new IndexModel(new FakeAdminApiClient(), NullLogger<IndexModel>.Instance);

        Assert.That(model.LandingState, Is.EqualTo(IndexModel.OperatorLandingState.Loading));
    }

    [Test]
    public async Task OperatorLandingPage_RendersSelectedCategoryConsole()
    {
        var fakeAdminApiClient = CreateLandingClient();

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var html = await client.GetStringAsync("/?category=monitor&selectedCategory=monitor&selectedCategory=tv");

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("Current Category Context"));
            Assert.That(html, Does.Contain("Monitors &#x2B;1 more"));
            Assert.That(html, Does.Contain("Quick Crawl Launch"));
            Assert.That(html, Does.Contain("Active Crawl Jobs"));
            Assert.That(html, Does.Contain("Product Counts"));
            Assert.That(html, Does.Contain("Quality Summary"));
            Assert.That(html, Does.Contain("Source Health Summary"));
            Assert.That(html, Does.Contain("Start Crawl"));
            Assert.That(html, Does.Contain("View Jobs"));
            Assert.That(html, Does.Contain("Explore Products"));
            Assert.That(html, Does.Contain("Review Quality"));
            Assert.That(html, Does.Contain("Manage Sources"));
            Assert.That(html, Does.Contain("Launch current category crawl"));
            Assert.That(html, Does.Contain("job_active_1"));
            Assert.That(html, Does.Contain("Canonical products"));
            Assert.That(html, Does.Contain("Schema readiness"));
            Assert.That(html, Does.Contain("Crawl-ready"));
            Assert.That(html, Does.Contain("Needs attention"));
            Assert.That(html, Does.Contain("Ready"));
            Assert.That(html, Does.Contain("Healthy"));
            Assert.That(html, Does.Contain("Last crawl succeeded"));
            Assert.That(html, Does.Contain("Last crawl failed"));
        });
    }

    [Test]
    public async Task OperatorLandingPage_RendersNoCategorySelectedState()
    {
        var fakeAdminApiClient = new FakeAdminApiClient
        {
            Categories = [],
            Sources = [],
            Stats = new StatsDto(),
            CrawlJobsPage = new CrawlJobListResponseDto { Page = 1, PageSize = 5, Items = [] }
        };

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var html = await client.GetStringAsync("/");

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("No category context is selected yet"));
            Assert.That(html, Does.Contain("Main operator paths"));
            Assert.That(html, Does.Contain("No active crawl jobs are running right now."));
            Assert.That(html, Does.Contain("No managed sources currently match the active category context."));
        });
    }

    [Test]
    public async Task OperatorLandingPage_RendersErrorState()
    {
        var fakeAdminApiClient = new FakeAdminApiClient
        {
            CategoriesException = new AdminApiException("Dashboard data is unavailable.")
        };

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var html = await client.GetStringAsync("/");

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("Dashboard data is unavailable."));
            Assert.That(html, Does.Contain("Primary Actions"));
            Assert.That(html, Does.Contain("No category context is selected yet").Or.Contain("No category context is active yet"));
        });
    }

    private static FakeAdminApiClient CreateLandingClient()
    {
        return new FakeAdminApiClient
        {
            Categories =
            [
                new CategoryMetadataDto
                {
                    CategoryKey = "tv",
                    DisplayName = "TVs",
                    FamilyKey = "display",
                    FamilyDisplayName = "Display",
                    IconKey = "tv",
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
                    IconKey = "monitor",
                    IsEnabled = true,
                    CrawlSupportStatus = "Supported",
                    SchemaCompletenessScore = 0.92m
                }
            ],
            CategoryDetail = new CategoryDetailDto
            {
                Metadata = new CategoryMetadataDto
                {
                    CategoryKey = "monitor",
                    DisplayName = "Monitors",
                    FamilyKey = "display",
                    FamilyDisplayName = "Display",
                    IconKey = "monitor",
                    IsEnabled = true,
                    CrawlSupportStatus = "Supported",
                    SchemaCompletenessScore = 0.92m
                },
                Schema = new CategorySchemaDto
                {
                    CategoryKey = "monitor",
                    DisplayName = "Monitors",
                    Attributes =
                    [
                        new CategorySchemaAttributeDto { Key = "refresh_rate_hz", DisplayName = "Refresh Rate", ValueType = "number", Unit = "Hz", IsRequired = true },
                        new CategorySchemaAttributeDto { Key = "panel_type", DisplayName = "Panel Type", ValueType = "string", IsRequired = true },
                        new CategorySchemaAttributeDto { Key = "resolution", DisplayName = "Resolution", ValueType = "string", IsRequired = true }
                    ]
                }
            },
            Sources =
            [
                new SourceDto
                {
                    SourceId = "ao_uk",
                    DisplayName = "AO UK",
                    IsEnabled = true,
                    SupportedCategoryKeys = ["tv", "monitor"],
                    ThrottlingPolicy = new SourceThrottlingPolicyDto { RequestsPerMinute = 30, RespectRobotsTxt = true },
                    Readiness = new SourceReadinessDto
                    {
                        Status = "Ready",
                        AssignedCategoryCount = 2,
                        CrawlableCategoryCount = 2,
                        Summary = "All 2 assigned categories are crawl-ready."
                    },
                    Health = new SourceHealthSummaryDto
                    {
                        Status = "Healthy",
                        TrustScore = 91m,
                        CoveragePercent = 86m,
                        SuccessfulCrawlRate = 97m,
                        SnapshotUtc = new DateTime(2026, 3, 23, 8, 0, 0, DateTimeKind.Utc)
                    },
                    LastActivity = new SourceLastActivityDto
                    {
                        TimestampUtc = new DateTime(2026, 3, 23, 9, 0, 0, DateTimeKind.Utc),
                        Status = "succeeded",
                        DurationMs = 1100,
                        ExtractedProductCount = 18,
                        HadMeaningfulChange = true,
                        MeaningfulChangeSummary = "Observed updated product content."
                    }
                },
                new SourceDto
                {
                    SourceId = "northwind",
                    DisplayName = "Northwind",
                    IsEnabled = true,
                    SupportedCategoryKeys = ["monitor"],
                    ThrottlingPolicy = new SourceThrottlingPolicyDto { RequestsPerMinute = 45, RespectRobotsTxt = true },
                    Readiness = new SourceReadinessDto
                    {
                        Status = "Limited",
                        AssignedCategoryCount = 1,
                        CrawlableCategoryCount = 1,
                        Summary = "Assigned category is crawlable, but health signals need review."
                    },
                    Health = new SourceHealthSummaryDto
                    {
                        Status = "Attention",
                        TrustScore = 63m,
                        CoveragePercent = 58m,
                        SuccessfulCrawlRate = 71m,
                        SnapshotUtc = new DateTime(2026, 3, 23, 8, 30, 0, DateTimeKind.Utc)
                    },
                    LastActivity = new SourceLastActivityDto
                    {
                        TimestampUtc = new DateTime(2026, 3, 23, 9, 15, 0, DateTimeKind.Utc),
                        Status = "failed",
                        DurationMs = 950,
                        ExtractedProductCount = 0,
                        HadMeaningfulChange = false,
                        ErrorMessage = "Navigation timeout."
                    }
                }
            ],
            Stats = new StatsDto
            {
                TotalCanonicalProducts = 128,
                TotalSourceProducts = 344,
                AverageAttributesPerProduct = 8.7m,
                PercentProductsWithConflicts = 18.5m,
                PercentProductsMissingKeyAttributes = 9.4m
            },
            CrawlJobsPage = new CrawlJobListResponseDto
            {
                Page = 1,
                PageSize = 5,
                Items =
                [
                    new CrawlJobDto
                    {
                        JobId = "job_active_1",
                        RequestType = "category",
                        RequestedCategories = ["monitor"],
                        TotalTargets = 12,
                        ProcessedTargets = 4,
                        Status = "running",
                        LastUpdatedAt = new DateTime(2026, 3, 23, 10, 0, 0, DateTimeKind.Utc)
                    }
                ]
            }
        };
    }
}