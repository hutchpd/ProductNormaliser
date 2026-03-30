using Microsoft.Playwright;
using ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Tests;

[TestFixture]
[Category("BrowserSmoke")]
[NonParallelizable]
public sealed class PlaywrightSmokeTests
{
    private IPlaywright? playwright;
    private IBrowser? browser;

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        playwright = await Playwright.CreateAsync();

        try
        {
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });
        }
        catch (PlaywrightException exception) when (IsMissingBrowserInstall(exception))
        {
            Assert.Ignore("Chromium is not installed for Playwright. Run 'pwsh ProductNormaliser.Web.Tests/bin/Debug/net10.0/playwright.ps1 install chromium' after building the web test project.");
        }
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync()
    {
        if (browser is not null)
        {
            await browser.DisposeAsync();
        }

        playwright?.Dispose();
    }

    [Test]
    public async Task SourcesDiscoveryRun_FlowsThroughBrowserToRunDetails()
    {
        var fakeAdminApiClient = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            CreatedDiscoveryRun = new DiscoveryRunDto
            {
                RunId = "discovery_run_browser",
                RequestedCategoryKeys = ["laptop", "tv"],
                Locale = "en-GB",
                Market = "UK",
                AutomationMode = "auto_accept_and_seed",
                Status = "queued",
                CurrentStage = "search",
                StatusMessage = "Discovery run is queued and waiting for worker capacity.",
                LlmStatus = "disabled",
                LlmStatusMessage = "LLM validation is disabled.",
                SearchResultCount = 8,
                CollapsedCandidateCount = 4,
                ProbeCompletedCount = 1,
                CreatedUtc = new DateTime(2026, 03, 27, 09, 30, 00, DateTimeKind.Utc),
                UpdatedUtc = new DateTime(2026, 03, 27, 09, 30, 00, DateTimeKind.Utc)
            }
        };

        await using var host = new BrowserProductWebHost(fakeAdminApiClient);
        await host.StartAsync();
        await using var context = await CreateLoggedInContextAsync(host, "/Sources");
        var page = await context.NewPageAsync();

        await page.GotoAsync(new Uri(host.RootUri, "/Sources").ToString());
        await page.Locator("input[name='CandidateDiscovery.CategoryKeys'][value='laptop']").CheckAsync();
        await page.Locator("input[name='CandidateDiscovery.CategoryKeys'][value='tv']").CheckAsync();
        await page.Locator("input[name='CandidateDiscovery.Locale']").FillAsync("en-GB");
        await page.Locator("input[name='CandidateDiscovery.Market']").FillAsync("UK");
        await page.Locator("select[name='CandidateDiscovery.AutomationMode']").SelectOptionAsync("auto_accept_and_seed");

        await page.GetByRole(AriaRole.Button, new() { Name = "Start discovery run" }).ClickAsync();
        await WaitForUrlContainsAsync(page, "/Sources/DiscoveryRuns/Details?runId=discovery_run_browser");

        var bodyText = await page.TextContentAsync("body") ?? string.Empty;

        Assert.Multiple(() =>
        {
            Assert.That(page.Url, Does.Contain("/Sources/DiscoveryRuns/Details?runId=discovery_run_browser"));
            Assert.That(bodyText, Does.Contain("discovery_run_browser"));
            Assert.That(bodyText, Does.Contain("This discovery run is active."));
            Assert.That(fakeAdminApiClient.LastCreateDiscoveryRunRequest, Is.Not.Null);
            Assert.That(fakeAdminApiClient.LastCreateDiscoveryRunRequest!.CategoryKeys, Is.EqualTo(new[] { "laptop", "tv" }));
        });
    }

    [Test]
    public async Task OperatorLanding_RequiredToggle_SavesThroughAjaxWithoutNavigation()
    {
        var fakeAdminApiClient = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            CategoryDetail = CreateCategoryDetail(),
            Sources =
            [
                CreateSource("ao_uk", "AO UK", ["tv"])
            ]
        };

        await using var host = new BrowserProductWebHost(fakeAdminApiClient);
        await host.StartAsync();
        await using var context = await CreateLoggedInContextAsync(host, "/?category=tv&selectedCategory=tv");
        var page = await context.NewPageAsync();

        await page.GotoAsync(new Uri(host.RootUri, "/?category=tv&selectedCategory=tv").ToString());
        var initialUrl = page.Url;

        await page.Locator("input[data-schema-required-toggle='true'][data-attribute-key='panel_type']").CheckAsync();
        await WaitForTextAsync(page.Locator("[data-schema-toggle-feedback]"), "Saved Panel Type as required for TVs.");

        var bodyText = await page.TextContentAsync("body") ?? string.Empty;

        Assert.Multiple(() =>
        {
            Assert.That(page.Url, Is.EqualTo(initialUrl));
            Assert.That(bodyText, Does.Contain("Track another field during discovery"));
            Assert.That(fakeAdminApiClient.LastUpdatedCategorySchemaCategoryKey, Is.EqualTo("tv"));
            Assert.That(fakeAdminApiClient.LastUpdatedCategorySchemaRequest, Is.Not.Null);
            Assert.That(fakeAdminApiClient.LastUpdatedCategorySchemaRequest!.Attributes.Any(attribute => attribute.Key == "panel_type" && attribute.IsRequired), Is.True);
        });
    }

    private async Task<IBrowserContext> CreateLoggedInContextAsync(BrowserProductWebHost host, string returnPath)
    {
        if (browser is null)
        {
            throw new InvalidOperationException("Playwright browser was not initialized.");
        }

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = host.RootUri.ToString()
        });

        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(host.RootUri, $"/Login?ReturnUrl={Uri.EscapeDataString(returnPath)}").ToString());
        await page.Locator("input[name='Input.Username']").FillAsync("operator");
        await page.Locator("input[name='Input.Password']").FillAsync("operator-pass");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
        await WaitForUrlContainsAsync(page, returnPath.TrimStart('/'));
        await page.CloseAsync();

        return context;
    }

    private static async Task WaitForUrlContainsAsync(IPage page, string expectedFragment, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            if (page.Url.Contains(expectedFragment, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await Task.Delay(100);
        }

        Assert.Fail($"Expected URL to contain '{expectedFragment}', but was '{page.Url}'.");
    }

    private static async Task WaitForTextAsync(ILocator locator, string expectedText, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            var text = await locator.TextContentAsync() ?? string.Empty;
            if (text.Contains(expectedText, StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(100);
        }

        Assert.Fail($"Expected text '{expectedText}' was not observed.");
    }

    private static bool IsMissingBrowserInstall(PlaywrightException exception)
    {
        return exception.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("Please run the following command", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("browser executable", StringComparison.OrdinalIgnoreCase);
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

    private static CategoryDetailDto CreateCategoryDetail()
    {
        return new CategoryDetailDto
        {
            Metadata = new CategoryMetadataDto
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
            Schema = new CategorySchemaDto
            {
                CategoryKey = "tv",
                DisplayName = "TVs",
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
                ProductUrlPatterns = ["/product/"],
                ListingUrlPatterns = ["/category/"]
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