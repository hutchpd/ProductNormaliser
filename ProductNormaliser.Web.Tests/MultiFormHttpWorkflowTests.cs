using System.Net;
using System.Text.RegularExpressions;
using ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Tests;

[TestFixture]
public sealed class MultiFormHttpWorkflowTests
{
    [Test]
    public async Task SourcesIndex_DiscoverCandidatesForm_PostsSuccessfully_WhenRegisterFormIsEmpty()
    {
        var fakeAdminApiClient = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            Sources = [],
            CreatedDiscoveryRun = new DiscoveryRunDto
            {
                RunId = "discovery_run_http",
                RequestedCategoryKeys = ["laptop", "tv"],
                Locale = "en-GB",
                Market = "UK",
                AutomationMode = "auto_accept_and_seed",
                Status = "queued",
                CurrentStage = "search",
                StatusMessage = "Discovery run is queued and waiting for worker capacity.",
                LlmStatus = "disabled",
                LlmStatusMessage = "LLM validation is disabled.",
                CreatedUtc = new DateTime(2026, 03, 27, 09, 00, 00, DateTimeKind.Utc),
                UpdatedUtc = new DateTime(2026, 03, 27, 09, 00, 00, DateTimeKind.Utc)
            }
        };

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var pageHtml = await client.GetStringAsync("/Sources");
        var requestVerificationToken = ExtractRequestVerificationToken(pageHtml);

        var response = await client.PostAsync("/Sources?handler=DiscoverCandidates", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("__RequestVerificationToken", requestVerificationToken),
            new KeyValuePair<string, string>("CandidateDiscovery.CategoryKeys", "laptop"),
            new KeyValuePair<string, string>("CandidateDiscovery.CategoryKeys", "tv"),
            new KeyValuePair<string, string>("CandidateDiscovery.Locale", "en-GB"),
            new KeyValuePair<string, string>("CandidateDiscovery.Market", "UK"),
            new KeyValuePair<string, string>("CandidateDiscovery.MaxCandidates", "10"),
            new KeyValuePair<string, string>("CandidateDiscovery.AutomationMode", "auto_accept_and_seed")
        ]));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(response.Headers.Location, Is.Not.Null);
            Assert.That(response.Headers.Location!.ToString(), Does.Contain("/Sources/DiscoveryRuns/Details"));
            Assert.That(response.Headers.Location!.ToString(), Does.Contain("runId=discovery_run_http"));
            Assert.That(fakeAdminApiClient.LastCreateDiscoveryRunRequest, Is.Not.Null);
            Assert.That(fakeAdminApiClient.LastCreateDiscoveryRunRequest!.CategoryKeys, Is.EqualTo(new[] { "laptop", "tv" }));
            Assert.That(fakeAdminApiClient.LastCreateDiscoveryRunRequest.Locale, Is.EqualTo("en-GB"));
            Assert.That(fakeAdminApiClient.LastCreateDiscoveryRunRequest.Market, Is.EqualTo("UK"));
        });
    }

    [Test]
    public async Task SourceDetails_ThrottlingForm_PostsSuccessfully_WhenIdentityFormIsEmpty()
    {
        var source = CreateSource("ao_uk", "AO UK", ["tv"]);
        var fakeAdminApiClient = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            Source = source,
            Sources = [source]
        };

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var pageHtml = await client.GetStringAsync("/Sources/Details/ao_uk");
        var requestVerificationToken = ExtractRequestVerificationToken(pageHtml);

        var response = await client.PostAsync("/Sources/Details/ao_uk?handler=Throttling", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("__RequestVerificationToken", requestVerificationToken),
            new KeyValuePair<string, string>("Throttling.MinDelayMs", "500"),
            new KeyValuePair<string, string>("Throttling.MaxDelayMs", "1500"),
            new KeyValuePair<string, string>("Throttling.MaxConcurrentRequests", "2"),
            new KeyValuePair<string, string>("Throttling.RequestsPerMinute", "20"),
            new KeyValuePair<string, string>("Throttling.RespectRobotsTxt", "true")
        ]));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(response.Headers.Location, Is.Not.Null);
            Assert.That(response.Headers.Location!.ToString(), Does.Contain("/Sources/Details/ao_uk"));
            Assert.That(fakeAdminApiClient.LastUpdatedThrottlingSourceId, Is.EqualTo("ao_uk"));
            Assert.That(fakeAdminApiClient.LastUpdatedThrottlingRequest, Is.Not.Null);
            Assert.That(fakeAdminApiClient.LastUpdatedThrottlingRequest!.MinDelayMs, Is.EqualTo(500));
            Assert.That(fakeAdminApiClient.LastUpdatedThrottlingRequest.MaxDelayMs, Is.EqualTo(1500));
            Assert.That(fakeAdminApiClient.LastUpdatedThrottlingRequest.MaxConcurrentRequests, Is.EqualTo(2));
            Assert.That(fakeAdminApiClient.LastUpdatedThrottlingRequest.RequestsPerMinute, Is.EqualTo(20));
            Assert.That(fakeAdminApiClient.LastUpdatedThrottlingRequest.RespectRobotsTxt, Is.True);
        });
    }

    [Test]
    public async Task SourceDetails_DiscoveryForm_PostsSuccessfully_WhenAllowedHostsIsEmpty()
    {
        var source = CreateSource("ao_uk", "AO UK", ["tv"]);
        var fakeAdminApiClient = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            Source = source,
            Sources = [source]
        };

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var pageHtml = await client.GetStringAsync("/Sources/Details/ao_uk");
        var requestVerificationToken = ExtractRequestVerificationToken(pageHtml);

        var response = await client.PostAsync("/Sources/Details/ao_uk?handler=Discovery", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("__RequestVerificationToken", requestVerificationToken),
            new KeyValuePair<string, string>("Discovery.CategoryEntryPages", "tv=/tv, /oled"),
            new KeyValuePair<string, string>("Discovery.SitemapHints", "/sitemap.xml"),
            new KeyValuePair<string, string>("Discovery.AllowedHosts", string.Empty),
            new KeyValuePair<string, string>("Discovery.AllowedPathPrefixes", "/tv\n/product"),
            new KeyValuePair<string, string>("Discovery.ExcludedPathPrefixes", "/support"),
            new KeyValuePair<string, string>("Discovery.ProductUrlPatterns", "/product/\n/p/"),
            new KeyValuePair<string, string>("Discovery.ListingUrlPatterns", "/category/\n/department/"),
            new KeyValuePair<string, string>("Discovery.MaxDiscoveryDepth", "4"),
            new KeyValuePair<string, string>("Discovery.MaxUrlsPerRun", "800"),
            new KeyValuePair<string, string>("Discovery.MaxRetryCount", "3"),
            new KeyValuePair<string, string>("Discovery.RetryBackoffBaseMs", "1000"),
            new KeyValuePair<string, string>("Discovery.RetryBackoffMaxMs", "30000")
        ]));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(response.Headers.Location, Is.Not.Null);
            Assert.That(response.Headers.Location!.ToString(), Does.Contain("/Sources/Details/ao_uk"));
            Assert.That(fakeAdminApiClient.LastUpdatedSourceId, Is.EqualTo("ao_uk"));
            Assert.That(fakeAdminApiClient.LastUpdatedSourceRequest, Is.Not.Null);
            Assert.That(fakeAdminApiClient.LastUpdatedSourceRequest!.DiscoveryProfile, Is.Not.Null);
            Assert.That(fakeAdminApiClient.LastUpdatedSourceRequest.DiscoveryProfile!.AllowedHosts, Is.Empty);
            Assert.That(fakeAdminApiClient.LastUpdatedSourceRequest.DiscoveryProfile.CategoryEntryPages["tv"], Is.EqualTo(new[] { "/tv", "/oled" }));
        });
    }

    [Test]
    public async Task OperatorLanding_SaveCategorySchema_PostsSuccessfully_WhenQuickCrawlFormIsEmpty()
    {
        var fakeAdminApiClient = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            CategoryDetail = CreateCategoryDetail("tv", "TVs"),
            Sources = [CreateSource("ao_uk", "AO UK", ["tv"])]
        };

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var pageHtml = await client.GetStringAsync("/?category=tv&selectedCategory=tv");
        var requestVerificationToken = ExtractRequestVerificationToken(pageHtml);

        var response = await client.PostAsync("/?handler=SaveCategorySchema&category=tv&selectedCategory=tv", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("__RequestVerificationToken", requestVerificationToken),
            new KeyValuePair<string, string>("CategorySchema.CategoryKey", "tv"),
            new KeyValuePair<string, string>("CategorySchema.Attributes[0].Key", "panel_type"),
            new KeyValuePair<string, string>("CategorySchema.Attributes[0].DisplayName", "Panel Type"),
            new KeyValuePair<string, string>("CategorySchema.Attributes[0].ValueType", "string"),
            new KeyValuePair<string, string>("CategorySchema.Attributes[0].Unit", string.Empty),
            new KeyValuePair<string, string>("CategorySchema.Attributes[0].ConflictSensitivity", "High"),
            new KeyValuePair<string, string>("CategorySchema.Attributes[0].Description", "Display panel technology."),
            new KeyValuePair<string, string>("CategorySchema.Attributes[0].IsRequired", "true")
        ]));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(response.Headers.Location, Is.Not.Null);
            Assert.That(response.Headers.Location!.ToString(), Does.Contain("category=tv"));
            Assert.That(fakeAdminApiClient.LastUpdatedCategorySchemaCategoryKey, Is.EqualTo("tv"));
            Assert.That(fakeAdminApiClient.LastUpdatedCategorySchemaRequest, Is.Not.Null);
            Assert.That(fakeAdminApiClient.LastUpdatedCategorySchemaRequest!.Attributes, Has.Count.EqualTo(1));
            Assert.That(fakeAdminApiClient.LastUpdatedCategorySchemaRequest.Attributes[0].Key, Is.EqualTo("panel_type"));
            Assert.That(fakeAdminApiClient.LastUpdatedCategorySchemaRequest.Attributes[0].IsRequired, Is.True);
        });
    }

    private static string ExtractRequestVerificationToken(string html)
    {
        var match = Regex.Match(
            html,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (!match.Success)
        {
            Assert.Fail("Expected page to render an antiforgery token.");
        }

        return WebUtility.HtmlDecode(match.Groups[1].Value);
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
                IconKey = "tv",
                IsEnabled = true,
                CrawlSupportStatus = "Supported",
                SchemaCompletenessScore = 0.95m
            },
            new CategoryMetadataDto
            {
                CategoryKey = "laptop",
                DisplayName = "Laptops",
                FamilyKey = "computing",
                FamilyDisplayName = "Computing",
                IconKey = "laptop",
                IsEnabled = true,
                CrawlSupportStatus = "Supported",
                SchemaCompletenessScore = 0.93m
            }
        ];
    }

    private static CategoryDetailDto CreateCategoryDetail(string categoryKey, string displayName)
    {
        return new CategoryDetailDto
        {
            Metadata = new CategoryMetadataDto
            {
                CategoryKey = categoryKey,
                DisplayName = displayName,
                FamilyKey = "display",
                FamilyDisplayName = "Display",
                IconKey = categoryKey,
                IsEnabled = true,
                CrawlSupportStatus = "Supported",
                SchemaCompletenessScore = 0.95m
            },
            Schema = new CategorySchemaDto
            {
                CategoryKey = categoryKey,
                DisplayName = displayName,
                Attributes =
                [
                    new CategorySchemaAttributeDto
                    {
                        Key = "panel_type",
                        DisplayName = "Panel Type",
                        ValueType = "string",
                        ConflictSensitivity = "High",
                        Description = "Display panel technology.",
                        IsRequired = false
                    }
                ]
            }
        };
    }

    private static SourceDto CreateSource(string sourceId, string displayName, IReadOnlyList<string> categoryKeys)
    {
        return new SourceDto
        {
            SourceId = sourceId,
            DisplayName = displayName,
            BaseUrl = $"https://{sourceId}.example/",
            Host = $"{sourceId}.example",
            Description = $"{displayName} source",
            IsEnabled = true,
            AllowedMarkets = ["UK"],
            PreferredLocale = "en-GB",
            AutomationPolicy = new SourceAutomationPolicyDto
            {
                Mode = "operator_assisted"
            },
            SupportedCategoryKeys = categoryKeys,
            DiscoveryProfile = new SourceDiscoveryProfileDto
            {
                AllowedMarkets = ["UK"],
                PreferredLocale = "en-GB",
                CategoryEntryPages = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    [categoryKeys[0]] = [$"https://{sourceId}.example/{categoryKeys[0]}"]
                },
                SitemapHints = [$"https://{sourceId}.example/sitemap.xml"],
                AllowedPathPrefixes = [$"/{categoryKeys[0]}", "/product"],
                ExcludedPathPrefixes = ["/support"],
                ProductUrlPatterns = ["/product/"],
                ListingUrlPatterns = ["/category/"],
                MaxDiscoveryDepth = 3,
                MaxUrlsPerRun = 500,
                MaxRetryCount = 3,
                RetryBackoffBaseMs = 1000,
                RetryBackoffMaxMs = 30000
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
                AssignedCategoryCount = categoryKeys.Count,
                CrawlableCategoryCount = categoryKeys.Count,
                Summary = $"All {categoryKeys.Count} assigned categories are crawl-ready."
            },
            Health = new SourceHealthSummaryDto
            {
                Status = "Healthy",
                TrustScore = 91m,
                CoveragePercent = 87m,
                SuccessfulCrawlRate = 93m,
                ExtractabilityRate = 81m,
                NoProductRate = 19m,
                Automation = new SourceAutomationPostureDto
                {
                    Status = "advisory",
                    EffectiveMode = "operator_assisted",
                    RecommendedAction = "none"
                }
            },
            CreatedUtc = new DateTime(2026, 03, 20, 10, 00, 00, DateTimeKind.Utc),
            UpdatedUtc = new DateTime(2026, 03, 27, 09, 00, 00, DateTimeKind.Utc)
        };
    }
}