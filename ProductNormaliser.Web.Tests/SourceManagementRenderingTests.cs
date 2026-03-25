using ProductNormaliser.Web.Contracts;
using System.Text.RegularExpressions;

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

    [Test]
    public async Task SourcesIndex_RendersCandidateDiscoveryResults_WithGovernanceAndDuplicateSignals()
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
                    SourceId = "currys_uk",
                    DisplayName = "Currys",
                    BaseUrl = "https://www.currys.co.uk/",
                    Host = "www.currys.co.uk",
                    IsEnabled = true,
                    SupportedCategoryKeys = ["tv"],
                    DiscoveryProfile = new SourceDiscoveryProfileDto(),
                    ThrottlingPolicy = new SourceThrottlingPolicyDto
                    {
                        MinDelayMs = 1000,
                        MaxDelayMs = 3000,
                        MaxConcurrentRequests = 1,
                        RequestsPerMinute = 30,
                        RespectRobotsTxt = true
                    },
                    Readiness = new SourceReadinessDto { Status = "Ready", AssignedCategoryCount = 1, CrawlableCategoryCount = 1, Summary = "Ready" },
                    Health = new SourceHealthSummaryDto { Status = "Healthy" },
                    CreatedUtc = DateTime.UtcNow,
                    UpdatedUtc = DateTime.UtcNow
                }
            ],
            SourceCandidateDiscoveryResponse = new SourceCandidateDiscoveryResponseDto
            {
                RequestedCategoryKeys = ["tv"],
                GeneratedUtc = new DateTime(2026, 03, 25, 12, 00, 00, DateTimeKind.Utc),
                Candidates =
                [
                    new SourceCandidateDto
                    {
                        CandidateKey = "currys_co_uk",
                        DisplayName = "Currys",
                        BaseUrl = "https://www.currys.co.uk/",
                        Host = "www.currys.co.uk",
                        CandidateType = "retailer",
                        ConfidenceScore = 82m,
                        MatchedCategoryKeys = ["tv"],
                        AlreadyRegistered = true,
                        DuplicateSourceIds = ["currys_uk"],
                        DuplicateSourceDisplayNames = ["Currys"],
                        AllowedByGovernance = false,
                        GovernanceWarning = "Governance review needed before registration.",
                        Probe = new SourceCandidateProbeDto
                        {
                            RobotsTxtReachable = true,
                            SitemapDetected = true,
                            SitemapUrls = ["https://www.currys.co.uk/sitemap.xml"]
                        },
                        Reasons =
                        [
                            new SourceCandidateReasonDto
                            {
                                Code = "duplicate",
                                Message = "Potential duplicate of a registered source.",
                                Weight = -40m
                            }
                        ]
                    }
                ]
            }
        };

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var initialHtml = await client.GetStringAsync("/Sources/Index?category=tv");
        var tokenMatch = Regex.Match(initialHtml, "<input name=\"__RequestVerificationToken\" type=\"hidden\" value=\"(?<token>[^\"]+)\"");
        Assert.That(tokenMatch.Success, Is.True, "Expected antiforgery token on sources index form.");

        var request = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = tokenMatch.Groups["token"].Value,
            ["CandidateDiscovery.CategoryKeys"] = "tv"
        });
        using var response = await client.PostAsync("/Sources/Index?handler=DiscoverCandidates", request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("Ephemeral source candidates"));
            Assert.That(html, Does.Contain("Matches registered source"));
            Assert.That(html, Does.Contain("Governance review needed before registration."));
            Assert.That(html, Does.Contain("Register accepted host below"));
            Assert.That(html, Does.Contain("Duplicate match: Currys"));
        });
    }
}