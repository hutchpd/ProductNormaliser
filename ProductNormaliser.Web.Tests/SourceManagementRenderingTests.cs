using ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Tests;

public sealed class SourceManagementRenderingTests
{
    [Test]
    public async Task SourcesIndex_RendersReadinessHealthAndInlineActions()
    {
        var fakeAdminApiClient = new FakeAdminApiClient
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
                    CrawlSupportStatus = "Supported",
                    SchemaCompletenessScore = 0.95m,
                    IsEnabled = true
                }
            ],
            Sources =
            [
                new SourceDto
                {
                    SourceId = "ao_uk",
                    DisplayName = "AO UK",
                    BaseUrl = "https://ao.example/",
                    Host = "ao.example",
                    Description = "TV source",
                    IsEnabled = true,
                    SupportedCategoryKeys = ["tv"],
                    ThrottlingPolicy = new SourceThrottlingPolicyDto
                    {
                        MinDelayMs = 1000,
                        MaxDelayMs = 4000,
                        MaxConcurrentRequests = 2,
                        RequestsPerMinute = 24,
                        RespectRobotsTxt = true
                    },
                    Readiness = new SourceReadinessDto
                    {
                        Status = "Ready",
                        AssignedCategoryCount = 1,
                        CrawlableCategoryCount = 1,
                        Summary = "All 1 assigned categories are crawl-ready."
                    },
                    Health = new SourceHealthSummaryDto
                    {
                        Status = "Healthy",
                        TrustScore = 91m,
                        CoveragePercent = 87m,
                        SuccessfulCrawlRate = 93m,
                        SnapshotUtc = new DateTime(2026, 03, 23, 08, 00, 00, DateTimeKind.Utc)
                    },
                    LastActivity = new SourceLastActivityDto
                    {
                        TimestampUtc = new DateTime(2026, 03, 23, 09, 00, 00, DateTimeKind.Utc),
                        Status = "succeeded",
                        DurationMs = 1830,
                        ExtractedProductCount = 12,
                        HadMeaningfulChange = true,
                        MeaningfulChangeSummary = "Detected updated specifications."
                    },
                    CreatedUtc = new DateTime(2026, 03, 20, 10, 00, 00, DateTimeKind.Utc),
                    UpdatedUtc = new DateTime(2026, 03, 23, 09, 15, 00, DateTimeKind.Utc)
                }
            ]
        };

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var html = await client.GetStringAsync("/Sources/Index?category=tv");

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("Register a new crawl host"));
            Assert.That(html, Does.Contain("Register source"));
            Assert.That(html, Does.Contain("Managed source hosts and health"));
            Assert.That(html, Does.Contain("Ready sources"));
            Assert.That(html, Does.Contain("AO UK"));
            Assert.That(html, Does.Contain("Healthy"));
            Assert.That(html, Does.Contain("Ready"));
            Assert.That(html, Does.Contain("Last crawl succeeded"));
            Assert.That(html, Does.Contain("24 rpm, 2 concurrent, 1000-4000 ms"));
            Assert.That(html, Does.Contain("Disable"));
            Assert.That(html, Does.Contain("Health"));
        });
    }

    [Test]
    public async Task SourceDetails_RendersDiscoveryProfileEditor()
    {
        var fakeAdminApiClient = new FakeAdminApiClient
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
                    CrawlSupportStatus = "Supported",
                    SchemaCompletenessScore = 0.95m,
                    IsEnabled = true
                }
            ],
            Sources =
            [
                new SourceDto
                {
                    SourceId = "ao_uk",
                    DisplayName = "AO UK",
                    BaseUrl = "https://ao.example/",
                    Host = "ao.example",
                    Description = "TV source",
                    IsEnabled = true,
                    SupportedCategoryKeys = ["tv"],
                    DiscoveryProfile = new SourceDiscoveryProfileDto
                    {
                        CategoryEntryPages = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["tv"] = ["https://ao.example/tv"]
                        },
                        SitemapHints = ["https://ao.example/sitemap.xml"],
                        AllowedPathPrefixes = ["/tv", "/product"],
                        ExcludedPathPrefixes = ["/support"],
                        ProductUrlPatterns = ["/product/"],
                        ListingUrlPatterns = ["/category/"],
                        MaxDiscoveryDepth = 3,
                        MaxUrlsPerRun = 500
                    },
                    ThrottlingPolicy = new SourceThrottlingPolicyDto
                    {
                        MinDelayMs = 1000,
                        MaxDelayMs = 4000,
                        MaxConcurrentRequests = 2,
                        RequestsPerMinute = 24,
                        RespectRobotsTxt = true
                    },
                    Readiness = new SourceReadinessDto
                    {
                        Status = "Ready",
                        AssignedCategoryCount = 1,
                        CrawlableCategoryCount = 1,
                        Summary = "All 1 assigned categories are crawl-ready."
                    },
                    Health = new SourceHealthSummaryDto
                    {
                        Status = "Healthy",
                        TrustScore = 91m,
                        CoveragePercent = 87m,
                        SuccessfulCrawlRate = 93m,
                        SnapshotUtc = new DateTime(2026, 03, 23, 08, 00, 00, DateTimeKind.Utc)
                    },
                    CreatedUtc = new DateTime(2026, 03, 20, 10, 00, 00, DateTimeKind.Utc),
                    UpdatedUtc = new DateTime(2026, 03, 23, 09, 15, 00, DateTimeKind.Utc)
                }
            ]
        };

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var html = await client.GetStringAsync("/Sources/Details/ao_uk");

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("Configure sitemap and listing discovery"));
            Assert.That(html, Does.Contain("Category entry pages"));
            Assert.That(html, Does.Contain("Sitemap hints"));
            Assert.That(html, Does.Contain("Save Discovery Profile"));
        });
    }
}