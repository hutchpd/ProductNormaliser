using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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
            Assert.That(html, Does.Contain("Operational Health"));
            Assert.That(html, Does.Contain("Runtime queue and failure posture"));
            Assert.That(html, Does.Contain("Monitors &#x2B;1 more"));
            Assert.That(html, Does.Contain("Quick Crawl Launch"));
            Assert.That(html, Does.Contain("Active Crawl Jobs"));
            Assert.That(html, Does.Contain("Product Counts"));
            Assert.That(html, Does.Contain("Quality Summary"));
            Assert.That(html, Does.Contain("Save attribute lens"));
            Assert.That(html, Does.Contain("Track another field during discovery"));
            Assert.That(html, Does.Contain("Source Health Summary"));
            Assert.That(html, Does.Contain("Queue depth"));
            Assert.That(html, Does.Contain("Retry backlog"));
            Assert.That(html, Does.Contain("At-Risk Sources"));
            Assert.That(html, Does.Contain("Category Pressure"));
            Assert.That(html, Does.Contain("Northwind"));
            Assert.That(html, Does.Contain("monitor"));
            Assert.That(html, Does.Contain("Start Crawl"));
            Assert.That(html, Does.Contain("View Jobs"));
            Assert.That(html, Does.Contain("Explore Products"));
            Assert.That(html, Does.Contain("Review Quality"));
            Assert.That(html, Does.Contain("Manage Sources"));
            Assert.That(html, Does.Contain("Investigate retries and failures"));
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
    public async Task OperatorLanding_OnPostSaveCategorySchemaAsync_SubmitsRequiredTogglesAndNewAttribute()
    {
        var fakeAdminApiClient = CreateLandingClient();
        var model = new IndexModel(fakeAdminApiClient, NullLogger<IndexModel>.Instance)
        {
            CategorySchema = new IndexModel.ManageCategorySchemaInput
            {
                CategoryKey = "tv",
                Attributes =
                [
                    new IndexModel.ManageCategorySchemaAttributeInput
                    {
                        Key = "brand",
                        DisplayName = "Brand",
                        ValueType = "string",
                        IsRequired = true,
                        ConflictSensitivity = "Critical",
                        Description = "Manufacturer brand name."
                    },
                    new IndexModel.ManageCategorySchemaAttributeInput
                    {
                        Key = "screen_size_inch",
                        DisplayName = "Screen Size",
                        ValueType = "decimal",
                        Unit = "inch",
                        IsRequired = true,
                        ConflictSensitivity = "High",
                        Description = "Nominal display size in inches."
                    }
                ],
                NewAttribute = new IndexModel.NewCategorySchemaAttributeInput
                {
                    DisplayName = "Display Port Count",
                    ValueType = "integer",
                    IsRequired = true,
                    ConflictSensitivity = "Medium",
                    Description = "Number of DisplayPort inputs."
                }
            },
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        model.PageContext.HttpContext.Request.QueryString = new QueryString("?category=tv&selectedCategory=tv");

        var result = await model.OnPostSaveCategorySchemaAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<RedirectToPageResult>());
            Assert.That(fakeAdminApiClient.LastUpdatedCategorySchemaCategoryKey, Is.EqualTo("tv"));
            Assert.That(fakeAdminApiClient.LastUpdatedCategorySchemaRequest, Is.Not.Null);
            Assert.That(fakeAdminApiClient.LastUpdatedCategorySchemaRequest!.Attributes.Select(attribute => attribute.Key), Does.Contain("display_port_count"));
            Assert.That(fakeAdminApiClient.LastUpdatedCategorySchemaRequest.Attributes.Single(attribute => attribute.Key == "screen_size_inch").IsRequired, Is.True);
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
                        ExtractabilityRate = 88m,
                        NoProductRate = 12m,
                        SnapshotUtc = new DateTime(2026, 3, 23, 8, 0, 0, DateTimeKind.Utc)
                    },
                    LastActivity = new SourceLastActivityDto
                    {
                        TimestampUtc = new DateTime(2026, 3, 23, 9, 0, 0, DateTimeKind.Utc),
                        Status = "succeeded",
                        ExtractionOutcome = "products_extracted",
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
                        ExtractabilityRate = 29m,
                        NoProductRate = 71m,
                        SnapshotUtc = new DateTime(2026, 3, 23, 8, 30, 0, DateTimeKind.Utc)
                    },
                    LastActivity = new SourceLastActivityDto
                    {
                        TimestampUtc = new DateTime(2026, 3, 23, 9, 15, 0, DateTimeKind.Utc),
                        Status = "failed",
                        ExtractionOutcome = "not_attempted",
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
                PercentProductsMissingKeyAttributes = 9.4m,
                Operational = new OperationalSummaryDto
                {
                    ActiveJobCount = 1,
                    QueueDepth = 6,
                    RetryQueueDepth = 2,
                    FailedQueueDepth = 1,
                    ThroughputLast24Hours = 31,
                    FailureCountLast24Hours = 4,
                    HealthySourceCount = 1,
                    AttentionSourceCount = 1,
                    Sources =
                    [
                        new SourceOperationalMetricDto
                        {
                            SourceName = "AO UK",
                            HealthStatus = "Healthy",
                            QueueDepth = 1,
                            RetryQueueDepth = 0,
                            FailedQueueDepth = 0,
                            TotalCrawlsLast24Hours = 18,
                            FailedCrawlsLast24Hours = 0,
                            FailureRateLast24Hours = 0m,
                            TrustScore = 91m,
                            CoveragePercent = 86m,
                            SuccessfulCrawlRate = 97m,
                            SnapshotUtc = new DateTime(2026, 3, 23, 8, 0, 0, DateTimeKind.Utc),
                            LastCrawlUtc = new DateTime(2026, 3, 23, 9, 0, 0, DateTimeKind.Utc)
                        },
                        new SourceOperationalMetricDto
                        {
                            SourceName = "Northwind",
                            HealthStatus = "Attention",
                            QueueDepth = 3,
                            RetryQueueDepth = 2,
                            FailedQueueDepth = 1,
                            TotalCrawlsLast24Hours = 13,
                            FailedCrawlsLast24Hours = 4,
                            FailureRateLast24Hours = 30.8m,
                            TrustScore = 63m,
                            CoveragePercent = 58m,
                            SuccessfulCrawlRate = 71m,
                            SnapshotUtc = new DateTime(2026, 3, 23, 8, 30, 0, DateTimeKind.Utc),
                            LastCrawlUtc = new DateTime(2026, 3, 23, 9, 15, 0, DateTimeKind.Utc)
                        }
                    ],
                    Categories =
                    [
                        new CategoryOperationalMetricDto
                        {
                            CategoryKey = "monitor",
                            ActiveJobCount = 1,
                            QueueDepth = 4,
                            RetryQueueDepth = 2,
                            ThroughputLast24Hours = 18,
                            FailedCrawlsLast24Hours = 3,
                            FailureRateLast24Hours = 16.7m,
                            DistinctSourceCount = 2
                        },
                        new CategoryOperationalMetricDto
                        {
                            CategoryKey = "tv",
                            ActiveJobCount = 0,
                            QueueDepth = 2,
                            RetryQueueDepth = 0,
                            ThroughputLast24Hours = 13,
                            FailedCrawlsLast24Hours = 1,
                            FailureRateLast24Hours = 7.7m,
                            DistinctSourceCount = 1
                        }
                    ]
                }
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