using ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Tests;

public sealed class SourceManagementRenderingTests
{
    [Test]
    public async Task SourcesIndex_RendersRecentDiscoveryRunHistoryWithCollapsedOlderRuns()
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
            DiscoveryRunPage = new DiscoveryRunPageDto
            {
                Items =
                [
                    CreateDiscoveryRunDto("discovery_run_1", status: "running"),
                    CreateDiscoveryRunDto("discovery_run_2", status: "completed"),
                    CreateDiscoveryRunDto("discovery_run_3", status: "completed"),
                    CreateDiscoveryRunDto("discovery_run_4", status: "failed")
                ],
                Page = 1,
                PageSize = 10,
                TotalCount = 4,
                TotalPages = 1
            }
        };

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var html = await client.GetStringAsync("/Sources/Index?category=tv");

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("Recent discovery runs"));
            Assert.That(html, Does.Contain("Manage recurring discovery campaigns"));
            Assert.That(html, Does.Contain("discovery_run_1"));
            Assert.That(html, Does.Contain("discovery_run_2"));
            Assert.That(html, Does.Contain("discovery_run_3"));
            Assert.That(html, Does.Contain("Show 1 older run(s)"));
            Assert.That(html, Does.Contain("discovery_run_4"));
        });
    }

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
                    AllowedMarkets = ["UK"],
                    PreferredLocale = "en-GB",
                    SupportedCategoryKeys = ["tv"],
                    DiscoveryProfile = new SourceDiscoveryProfileDto
                    {
                        AllowedMarkets = ["UK"],
                        PreferredLocale = "en-GB"
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
                        ExtractabilityRate = 82m,
                        NoProductRate = 18m,
                        Automation = new SourceAutomationPostureDto
                        {
                            Status = "downgraded",
                            EffectiveMode = "suggest_accept",
                            RecommendedAction = "downgrade_to_suggest",
                            SnapshotCount = 4,
                            DiscoveryBreadthScore = 68m,
                            ProductTargetPromotionRate = 57m,
                            DownstreamYieldScore = 62m,
                            BlockingReasons = ["Trust trend moved -12 points over the monitoring window."]
                        },
                        SnapshotUtc = new DateTime(2026, 03, 23, 08, 00, 00, DateTimeKind.Utc)
                    },
                    LastActivity = new SourceLastActivityDto
                    {
                        TimestampUtc = new DateTime(2026, 03, 23, 09, 00, 00, DateTimeKind.Utc),
                        Status = "succeeded",
                        ExtractionOutcome = "products_extracted",
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
            Assert.That(html, Does.Contain("LLM validation disabled"));
            Assert.That(html, Does.Contain("Discovery uses heuristics only"));
            Assert.That(html, Does.Contain("Managed source hosts and health"));
            Assert.That(html, Does.Contain("Ready sources"));
            Assert.That(html, Does.Contain("AO UK"));
            Assert.That(html, Does.Contain("en-GB"));
            Assert.That(html, Does.Contain("Healthy"));
            Assert.That(html, Does.Contain("Automation downgraded"));
            Assert.That(html, Does.Contain("downgrade to suggest-only"));
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
                    AllowedMarkets = ["UK", "IE"],
                    PreferredLocale = "en-GB",
                    SupportedCategoryKeys = ["tv"],
                    DiscoveryProfile = new SourceDiscoveryProfileDto
                    {
                        AllowedMarkets = ["UK", "IE"],
                        PreferredLocale = "en-GB",
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
                        ExtractabilityRate = 82m,
                        NoProductRate = 18m,
                        Automation = new SourceAutomationPostureDto
                        {
                            Status = "healthy",
                            EffectiveMode = "auto_accept_and_seed",
                            RecommendedAction = "keep_current_mode",
                            SnapshotCount = 5,
                            DiscoveryBreadthScore = 76m,
                            ProductTargetPromotionRate = 71m,
                            DownstreamYieldScore = 67m,
                            SupportingReasons = ["Longitudinal evidence still supports guarded auto-accept."]
                        },
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
            Assert.That(html, Does.Contain("Allowed markets"));
            Assert.That(html, Does.Contain("Preferred locale"));
            Assert.That(html, Does.Contain("Category entry pages"));
            Assert.That(html, Does.Contain("Sitemap hints"));
            Assert.That(html, Does.Contain("Save Discovery Profile"));
            Assert.That(html, Does.Contain("Extractability"));
            Assert.That(html, Does.Contain("No-product rate"));
            Assert.That(html, Does.Contain("Effective automation mode"));
            Assert.That(html, Does.Contain("Longitudinal evidence still supports guarded auto-accept."));
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
                    AllowedMarkets = ["UK"],
                    PreferredLocale = "en-GB",
                    SupportedCategoryKeys = ["tv"],
                    DiscoveryProfile = new SourceDiscoveryProfileDto
                    {
                        AllowedMarkets = ["UK"],
                        PreferredLocale = "en-GB"
                    },
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
            CreatedDiscoveryRun = CreateDiscoveryRunDto(
                runId: "discovery_run_currys",
                llmStatus: "unconfigured",
                llmStatusMessage: "LLM validation is enabled, but the local GGUF model file was not found. Discovery uses heuristics only."),
            DiscoveryRun = CreateDiscoveryRunDto(
                runId: "discovery_run_currys",
                llmStatus: "unconfigured",
                llmStatusMessage: "LLM validation is enabled, but the local GGUF model file was not found. Discovery uses heuristics only."),
            DiscoveryRunCandidates =
            [
                CreateDiscoveryRunCandidateDto(
                    candidateKey: "currys_co_uk",
                    displayName: "Currys",
                    state: "failed",
                    stateMessage: "Governance review needed before registration.",
                    confidenceScore: 82m,
                    crawlabilityScore: 40m,
                    extractabilityScore: 10m,
                    recommendationStatus: "do_not_accept",
                    runtimeExtractionStatus: "not_compatible",
                    runtimeExtractionMessage: "Representative runtime extraction did not produce products from the sampled product page.")
            ]
        };

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var html = await client.GetStringAsync("/Sources/DiscoveryRuns/Details?runId=discovery_run_currys");

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("Discovery Run"));
            Assert.That(html, Does.Contain("Currys"));
            Assert.That(html, Does.Contain("Governance review needed before registration."));
            Assert.That(html, Does.Contain("Dismiss"));
            Assert.That(html, Does.Contain("LLM status"));
            Assert.That(html, Does.Contain("discovery_run_currys"));
            Assert.That(html, Does.Contain("Activity log"));
            Assert.That(html, Does.Contain("Archived and rejected candidates"));
            Assert.That(html, Does.Not.Contain("<button type=\"submit\" class=\"btn btn-dark\">Accept candidate</button>"));
        });
    }

    [Test]
    public async Task SourcesIndex_RendersAcceptCandidateAction_ForGovernanceApprovedUnregisteredCandidate()
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
            Sources = [],
            CreatedDiscoveryRun = CreateDiscoveryRunDto(runId: "discovery_run_richer_sounds"),
            DiscoveryRun = CreateDiscoveryRunDto(runId: "discovery_run_richer_sounds"),
            DiscoveryRunCandidates =
            [
                CreateDiscoveryRunCandidateDto(
                    candidateKey: "richersounds_co_uk",
                    displayName: "Richer Sounds",
                    state: "suggested",
                    stateMessage: "Ready for operator review.",
                    confidenceScore: 88m,
                    crawlabilityScore: 85m,
                    extractabilityScore: 82m,
                    recommendationStatus: "recommended",
                    runtimeExtractionStatus: "compatible",
                    runtimeExtractionMessage: "Representative runtime extraction produced products from the sampled product page.")
            ]
        };

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var html = await client.GetStringAsync("/Sources/DiscoveryRuns/Details?runId=discovery_run_richer_sounds");

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("Discovery Run"));
            Assert.That(html, Does.Contain("Richer Sounds"));
            Assert.That(html, Does.Contain("Accept candidate"));
            Assert.That(html, Does.Contain("Ready for operator review."));
        });
    }

    [Test]
    public async Task SourcesIndex_RendersManualReviewCandidateState()
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
            Sources = [],
            CreatedDiscoveryRun = CreateDiscoveryRunDto(runId: "discovery_run_manual_review"),
            DiscoveryRun = CreateDiscoveryRunDto(runId: "discovery_run_manual_review"),
            DiscoveryRunCandidates =
            [
                CreateDiscoveryRunCandidateDto(
                    candidateKey: "manual_review_candidate",
                    displayName: "Manual Review Candidate",
                    state: "suggested",
                    stateMessage: "Ready for operator review.",
                    confidenceScore: 49m,
                    crawlabilityScore: 70m,
                    extractabilityScore: 30m,
                    recommendationStatus: "manual_review",
                    runtimeExtractionStatus: "not_compatible",
                    runtimeExtractionMessage: "Representative runtime extraction did not produce products from the sampled product page.")
            ]
        };

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var html = await client.GetStringAsync("/Sources/DiscoveryRuns/Details?runId=discovery_run_manual_review");

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("Discovery Run"));
            Assert.That(html, Does.Contain("Manual Review Candidate"));
            Assert.That(html, Does.Contain("Representative runtime extraction did not produce products from the sampled product page."));
            Assert.That(html, Does.Contain("Accept candidate"));
            Assert.That(html, Does.Contain("Dismiss"));
        });
    }

    private static DiscoveryRunDto CreateDiscoveryRunDto(
        string runId,
        string status = "running",
        string stage = "decide",
        string llmStatus = "disabled",
        string llmStatusMessage = "LLM validation is disabled.")
    {
        return new DiscoveryRunDto
        {
            RunId = runId,
            RequestedCategoryKeys = ["tv"],
            AutomationMode = "operator_assisted",
            MaxCandidates = 10,
            Status = status,
            CurrentStage = stage,
            StatusMessage = "Discovery run is ready for operator review.",
            LlmStatus = llmStatus,
            LlmStatusMessage = llmStatusMessage,
            SearchResultCount = 1,
            CollapsedCandidateCount = 1,
            ProbeCompletedCount = 1,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
            StartedUtc = DateTime.UtcNow.AddMinutes(-1)
        };
    }

    private static DiscoveryRunCandidateDto CreateDiscoveryRunCandidateDto(
        string candidateKey,
        string displayName,
        string state,
        string stateMessage,
        decimal confidenceScore,
        decimal crawlabilityScore,
        decimal extractabilityScore,
        string recommendationStatus,
        string runtimeExtractionStatus,
        string runtimeExtractionMessage)
    {
        return new DiscoveryRunCandidateDto
        {
            CandidateKey = candidateKey,
            Revision = 1,
            State = state,
            StateMessage = stateMessage,
            DisplayName = displayName,
            BaseUrl = $"https://{candidateKey}.example/",
            Host = $"{candidateKey}.example",
            CandidateType = "retailer",
            ConfidenceScore = confidenceScore,
            CrawlabilityScore = crawlabilityScore,
            ExtractabilityScore = extractabilityScore,
            RecommendationStatus = recommendationStatus,
            RuntimeExtractionStatus = runtimeExtractionStatus,
            RuntimeExtractionMessage = runtimeExtractionMessage,
            MatchedCategoryKeys = ["tv"],
            AllowedByGovernance = true,
            Probe = new SourceCandidateProbeDto(),
            AutomationAssessment = new SourceCandidateAutomationAssessmentDto { RequestedMode = "operator_assisted", Decision = "manual_only", MarketEvidence = "unknown", LocaleEvidence = "unknown" },
            Reasons = []
        };
    }
}