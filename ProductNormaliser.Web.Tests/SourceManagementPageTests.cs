using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Models;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Tests;

public sealed class SourceManagementPageTests
{
    [Test]
    public async Task SourcesIndex_OnGetAsync_FiltersByPersistedCategorySearchAndEnabled()
    {
        var client = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            Sources =
            [
                CreateSource("ao_uk", "AO UK", isEnabled: true, categoryKeys: ["tv"], readinessStatus: "Ready", healthStatus: "Healthy"),
                CreateSource("laptop_world", "Laptop World", isEnabled: false, categoryKeys: ["laptop"], readinessStatus: "Blocked", healthStatus: "Attention")
            ]
        };

        var model = new ProductNormaliser.Web.Pages.Sources.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.IndexModel>.Instance)
        {
            Search = "AO",
            Enabled = true,
            PageContext = new PageContext
            {
                HttpContext = CreateHttpContextWithCategoryCookie("tv")
            }
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(model.CategoryKey, Is.EqualTo("tv"));
            Assert.That(model.TotalSources, Is.EqualTo(2));
            Assert.That(model.Sources.Select(source => source.SourceId), Is.EqualTo(new[] { "ao_uk" }));
            Assert.That(model.ReadySources, Is.EqualTo(1));
            Assert.That(model.AttentionSources, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task SourcesIndex_OnPostToggleAsync_DisablesSourceAndPreservesFilters()
    {
        var client = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            Sources = [CreateSource("ao_uk", "AO UK", isEnabled: true, categoryKeys: ["tv"], readinessStatus: "Ready", healthStatus: "Healthy")]
        };

        var model = new ProductNormaliser.Web.Pages.Sources.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.IndexModel>.Instance);

        var result = await model.OnPostToggleAsync("ao_uk", currentlyEnabled: true, category: "tv", search: "AO", enabled: true, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(client.LastDisabledSourceId, Is.EqualTo("ao_uk"));
            Assert.That(result, Is.TypeOf<RedirectToPageResult>());
            var redirect = (RedirectToPageResult)result;
            Assert.That(redirect.RouteValues!["category"], Is.EqualTo("tv"));
            Assert.That(redirect.RouteValues["search"], Is.EqualTo("AO"));
            Assert.That(redirect.RouteValues["enabled"], Is.EqualTo(true));
        });
    }

    [Test]
    public async Task SourcesIndex_OnPostRegisterAsync_RegistersSourceAndRedirectsToDetails()
    {
        var client = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            Sources = []
        };

        var model = new ProductNormaliser.Web.Pages.Sources.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.IndexModel>.Instance)
        {
            Registration = new ProductNormaliser.Web.Pages.Sources.IndexModel.RegisterSourceInput
            {
                SourceId = "northwind",
                DisplayName = "Northwind",
                BaseUrl = "https://www.northwind.example/",
                CategoryKeys = ["tv", "monitor"],
                IsEnabled = true
            }
        };

        var result = await model.OnPostRegisterAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(client.LastRegisteredSourceRequest, Is.Not.Null);
            Assert.That(client.LastRegisteredSourceRequest!.SupportedCategoryKeys, Is.EqualTo(new[] { "tv", "monitor" }));
            Assert.That(result, Is.TypeOf<RedirectToPageResult>());
            Assert.That(((RedirectToPageResult)result).PageName, Is.EqualTo("/Sources/Details"));
        });
    }

    [Test]
    public async Task SourcesIndex_OnPostDiscoverCandidatesAsync_LoadsEphemeralCandidateResults()
    {
        var client = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            SourceCandidateDiscoveryResponse = new SourceCandidateDiscoveryResponseDto
            {
                RequestedCategoryKeys = ["tv"],
                GeneratedUtc = new DateTime(2026, 03, 25, 11, 00, 00, DateTimeKind.Utc),
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
                        Probe = new SourceCandidateProbeDto(),
                        Reasons = []
                    }
                ]
            }
        };

        var model = new ProductNormaliser.Web.Pages.Sources.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.IndexModel>.Instance)
        {
            CandidateDiscovery = new ProductNormaliser.Web.Pages.Sources.IndexModel.DiscoverSourceCandidatesInput
            {
                CategoryKeys = ["tv"],
                Locale = "en-GB",
                BrandHints = "Samsung"
            }
        };

        var result = await model.OnPostDiscoverCandidatesAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<PageResult>());
            Assert.That(client.LastSourceCandidateDiscoveryRequest, Is.Not.Null);
            Assert.That(client.LastSourceCandidateDiscoveryRequest!.CategoryKeys, Is.EqualTo(new[] { "tv" }));
            Assert.That(model.CandidateDiscoveryResult, Is.Not.Null);
            Assert.That(model.CandidateDiscoveryResult!.Candidates.Select(candidate => candidate.DisplayName), Is.EqualTo(new[] { "Currys" }));
        });
    }

    [Test]
    public async Task SourcesIndex_OnPostDiscoverCandidatesAsync_UsesCategoryQueryFallback()
    {
        var client = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            SourceCandidateDiscoveryResponse = new SourceCandidateDiscoveryResponseDto
            {
                RequestedCategoryKeys = ["tv"],
                GeneratedUtc = DateTime.UtcNow,
                Candidates = []
            }
        };

        var model = new ProductNormaliser.Web.Pages.Sources.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.IndexModel>.Instance)
        {
            CategoryKey = "tv",
            CandidateDiscovery = new ProductNormaliser.Web.Pages.Sources.IndexModel.DiscoverSourceCandidatesInput
            {
                CategoryKeys = [],
                BrandHints = "Samsung"
            }
        };

        var result = await model.OnPostDiscoverCandidatesAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<PageResult>());
            Assert.That(client.LastSourceCandidateDiscoveryRequest, Is.Not.Null);
            Assert.That(client.LastSourceCandidateDiscoveryRequest!.CategoryKeys, Is.EqualTo(new[] { "tv" }));
            Assert.That(model.CandidateDiscovery.CategoryKeys, Is.EqualTo(new[] { "tv" }));
        });
    }

    [Test]
    public async Task SourcesIndex_OnPostUseCandidateAsync_PrefillsRegistrationFromCandidate()
    {
        var client = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            SourceCandidateDiscoveryResponse = new SourceCandidateDiscoveryResponseDto
            {
                RequestedCategoryKeys = ["tv", "monitor"],
                GeneratedUtc = new DateTime(2026, 03, 25, 11, 00, 00, DateTimeKind.Utc),
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
                        MatchedCategoryKeys = ["tv", "monitor"],
                        Probe = new SourceCandidateProbeDto(),
                        Reasons = []
                    }
                ]
            }
        };

        var model = new ProductNormaliser.Web.Pages.Sources.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.IndexModel>.Instance)
        {
            CandidateDiscovery = new ProductNormaliser.Web.Pages.Sources.IndexModel.DiscoverSourceCandidatesInput
            {
                CategoryKeys = ["tv", "monitor"],
                Locale = "en-GB",
                Market = "UK",
                BrandHints = "Samsung",
                MaxCandidates = 8
            },
            CandidateSelection = new ProductNormaliser.Web.Pages.Sources.IndexModel.UseCandidateInput
            {
                CandidateKey = "currys_co_uk",
                DisplayName = "Currys",
                BaseUrl = "https://www.currys.co.uk/",
                CategoryKeys = ["tv", "monitor"]
            },
            Registration = new ProductNormaliser.Web.Pages.Sources.IndexModel.RegisterSourceInput
            {
                IsEnabled = true
            }
        };

        var result = await model.OnPostUseCandidateAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<PageResult>());
            Assert.That(client.LastSourceCandidateDiscoveryRequest, Is.Not.Null);
            Assert.That(model.CandidateDiscoveryResult, Is.Not.Null);
            Assert.That(model.Registration.SourceId, Is.EqualTo("currys_co_uk"));
            Assert.That(model.Registration.DisplayName, Is.EqualTo("Currys"));
            Assert.That(model.Registration.BaseUrl, Is.EqualTo("https://www.currys.co.uk/"));
            Assert.That(model.Registration.CategoryKeys, Is.EqualTo(new[] { "monitor", "tv" }));
        });
    }

    [Test]
    public async Task SourcesIndex_OnPostRegisterAsync_SubmitsEditedPrefilledRegistration()
    {
        var client = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            Sources = []
        };

        var model = new ProductNormaliser.Web.Pages.Sources.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.IndexModel>.Instance)
        {
            CandidateSelection = new ProductNormaliser.Web.Pages.Sources.IndexModel.UseCandidateInput
            {
                CandidateKey = "currys_co_uk",
                DisplayName = "Currys",
                BaseUrl = "https://www.currys.co.uk/",
                CategoryKeys = ["tv"]
            },
            CandidateDiscovery = new ProductNormaliser.Web.Pages.Sources.IndexModel.DiscoverSourceCandidatesInput
            {
                CategoryKeys = ["tv"]
            },
            Registration = new ProductNormaliser.Web.Pages.Sources.IndexModel.RegisterSourceInput
            {
                IsEnabled = true
            }
        };

        await model.OnPostUseCandidateAsync(CancellationToken.None);

        model.Registration.SourceId = "currys_uk";
        model.Registration.DisplayName = "Currys UK";
        model.Registration.BaseUrl = "https://www.currys.co.uk/";
        model.Registration.CategoryKeys = ["tv", "monitor"];

        var result = await model.OnPostRegisterAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<RedirectToPageResult>());
            Assert.That(client.LastRegisteredSourceRequest, Is.Not.Null);
            Assert.That(client.LastRegisteredSourceRequest!.SourceId, Is.EqualTo("currys_uk"));
            Assert.That(client.LastRegisteredSourceRequest.DisplayName, Is.EqualTo("Currys UK"));
            Assert.That(client.LastRegisteredSourceRequest.SupportedCategoryKeys, Is.EqualTo(new[] { "tv", "monitor" }));
        });
    }

    [Test]
    public async Task SourcesIndex_OnPostDiscoverCandidatesAsync_AddsModelErrorsForValidationProblem()
    {
        var client = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            SourceCandidateDiscoveryException = new AdminApiValidationException(
                "Validation failed.",
                new Dictionary<string, string[]>
                {
                    ["request"] = ["Choose at least one category before discovering source candidates."]
                })
        };

        var model = new ProductNormaliser.Web.Pages.Sources.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.IndexModel>.Instance)
        {
            CandidateDiscovery = new ProductNormaliser.Web.Pages.Sources.IndexModel.DiscoverSourceCandidatesInput
            {
                CategoryKeys = ["tv"]
            }
        };

        var result = await model.OnPostDiscoverCandidatesAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<PageResult>());
            Assert.That(model.ModelState[string.Empty]!.Errors.Select(error => error.ErrorMessage), Does.Contain("Choose at least one category before discovering source candidates."));
            Assert.That(model.CandidateDiscoveryResult, Is.Null);
        });
    }

    [Test]
    public async Task SourcesIndex_OnPostDiscoverCandidatesAsync_SetsErrorMessageForAdminApiFailure()
    {
        var client = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            SourceCandidateDiscoveryException = new AdminApiException("Candidate discovery is temporarily unavailable.")
        };

        var model = new ProductNormaliser.Web.Pages.Sources.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.IndexModel>.Instance)
        {
            CandidateDiscovery = new ProductNormaliser.Web.Pages.Sources.IndexModel.DiscoverSourceCandidatesInput
            {
                CategoryKeys = ["tv"]
            }
        };

        var result = await model.OnPostDiscoverCandidatesAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<PageResult>());
            Assert.That(model.CandidateDiscoveryErrorMessage, Is.EqualTo("Candidate discovery is temporarily unavailable."));
            Assert.That(model.CandidateDiscoveryResult, Is.Null);
        });
    }

    [Test]
    public async Task SourceDetails_OnPostCategoriesAsync_SubmitsAssignmentsAndRedirects()
    {
        var client = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            Sources = [CreateSource("ao_uk", "AO UK", isEnabled: true, categoryKeys: ["tv"], readinessStatus: "Ready", healthStatus: "Healthy")]
        };

        var model = new ProductNormaliser.Web.Pages.Sources.DetailsModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.DetailsModel>.Instance)
        {
            CategoriesForm = new ProductNormaliser.Web.Pages.Sources.DetailsModel.CategoryAssignmentInput
            {
                CategoryKeys = ["tv", "monitor"]
            }
        };

        var result = await model.OnPostCategoriesAsync("ao_uk", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(client.LastAssignedCategoriesSourceId, Is.EqualTo("ao_uk"));
            Assert.That(client.LastAssignedCategoriesRequest, Is.Not.Null);
            Assert.That(client.LastAssignedCategoriesRequest!.CategoryKeys, Is.EqualTo(new[] { "tv", "monitor" }));
            Assert.That(result, Is.TypeOf<RedirectToPageResult>());
        });
    }

    [Test]
    public async Task SourceDetails_OnPostThrottlingAsync_SubmitsUpdatedPolicyAndRedirects()
    {
        var client = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            Sources = [CreateSource("ao_uk", "AO UK", isEnabled: true, categoryKeys: ["tv"], readinessStatus: "Ready", healthStatus: "Healthy")]
        };

        var model = new ProductNormaliser.Web.Pages.Sources.DetailsModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.DetailsModel>.Instance)
        {
            Throttling = new ProductNormaliser.Web.Pages.Sources.DetailsModel.ThrottlingInput
            {
                MinDelayMs = 1500,
                MaxDelayMs = 4500,
                MaxConcurrentRequests = 2,
                RequestsPerMinute = 20,
                RespectRobotsTxt = true
            }
        };

        var result = await model.OnPostThrottlingAsync("ao_uk", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(client.LastUpdatedThrottlingSourceId, Is.EqualTo("ao_uk"));
            Assert.That(client.LastUpdatedThrottlingRequest, Is.Not.Null);
            Assert.That(client.LastUpdatedThrottlingRequest!.RequestsPerMinute, Is.EqualTo(20));
            Assert.That(result, Is.TypeOf<RedirectToPageResult>());
        });
    }

    [Test]
    public async Task SourceDetails_OnPostDiscoveryAsync_SubmitsDiscoveryProfileAndRedirects()
    {
        var client = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            Sources = [CreateSource("ao_uk", "AO UK", isEnabled: true, categoryKeys: ["tv"], readinessStatus: "Ready", healthStatus: "Healthy")]
        };

        var model = new ProductNormaliser.Web.Pages.Sources.DetailsModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.DetailsModel>.Instance)
        {
            Discovery = new ProductNormaliser.Web.Pages.Sources.DetailsModel.DiscoveryProfileInput
            {
                CategoryEntryPages = "tv=/tv, /oled",
                SitemapHints = "/sitemap.xml\n/sitemap-products.xml",
                AllowedPathPrefixes = "/tv\n/product",
                ExcludedPathPrefixes = "/support",
                ProductUrlPatterns = "/product/\n/p/",
                ListingUrlPatterns = "/category/\n/department/",
                MaxDiscoveryDepth = 4,
                MaxUrlsPerRun = 800
            }
        };

        var result = await model.OnPostDiscoveryAsync("ao_uk", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(client.LastUpdatedSourceId, Is.EqualTo("ao_uk"));
            Assert.That(client.LastUpdatedSourceRequest, Is.Not.Null);
            Assert.That(client.LastUpdatedSourceRequest!.DiscoveryProfile, Is.Not.Null);
            Assert.That(client.LastUpdatedSourceRequest.DiscoveryProfile!.CategoryEntryPages["tv"], Is.EqualTo(new[] { "/tv", "/oled" }));
            Assert.That(client.LastUpdatedSourceRequest.DiscoveryProfile.MaxUrlsPerRun, Is.EqualTo(800));
            Assert.That(result, Is.TypeOf<RedirectToPageResult>());
        });
    }

    [Test]
    public async Task SourceDetails_OnPostSaveNoteAsync_SavesSourceNote()
    {
        var client = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            Sources = [CreateSource("ao_uk", "AO UK", isEnabled: true, categoryKeys: ["tv"], readinessStatus: "Ready", healthStatus: "Healthy")]
        };

        var model = new ProductNormaliser.Web.Pages.Sources.DetailsModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.DetailsModel>.Instance)
        {
            NoteInput = new ProductNormaliser.Web.Pages.Sources.DetailsModel.AnalystNoteInput
            {
                Title = "Check crawl timing",
                Content = "Review timeout profile before next pricing crawl."
            }
        };

        var result = await model.OnPostSaveNoteAsync("ao_uk", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(client.LastSavedAnalystNoteRequest, Is.Not.Null);
            Assert.That(client.LastSavedAnalystNoteRequest!.TargetType, Is.EqualTo("source"));
            Assert.That(client.LastSavedAnalystNoteRequest.TargetId, Is.EqualTo("ao_uk"));
            Assert.That(result, Is.TypeOf<RedirectToPageResult>());
        });
    }

    private static DefaultHttpContext CreateHttpContextWithCategoryCookie(string categoryKey)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = $"{CategoryContextState.CookieName}={categoryKey}";
        return httpContext;
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
                CrawlSupportStatus = "Supported",
                SchemaCompletenessScore = 0.95m,
                IsEnabled = true
            },
            new CategoryMetadataDto
            {
                CategoryKey = "monitor",
                DisplayName = "Monitors",
                FamilyKey = "display",
                FamilyDisplayName = "Display",
                IconKey = "monitor",
                CrawlSupportStatus = "Experimental",
                SchemaCompletenessScore = 0.92m,
                IsEnabled = true
            },
            new CategoryMetadataDto
            {
                CategoryKey = "laptop",
                DisplayName = "Laptops",
                FamilyKey = "computing",
                FamilyDisplayName = "Computing",
                IconKey = "laptop",
                CrawlSupportStatus = "Planned",
                SchemaCompletenessScore = 0.40m,
                IsEnabled = false
            }
        ];
    }

    private static SourceDto CreateSource(string sourceId, string displayName, bool isEnabled, IReadOnlyList<string> categoryKeys, string readinessStatus, string healthStatus)
    {
        return new SourceDto
        {
            SourceId = sourceId,
            DisplayName = displayName,
            BaseUrl = $"https://{sourceId}.example/",
            Host = $"{sourceId}.example",
            Description = $"{displayName} source",
            IsEnabled = isEnabled,
            SupportedCategoryKeys = categoryKeys,
            DiscoveryProfile = new SourceDiscoveryProfileDto
            {
                CategoryEntryPages = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    [categoryKeys[0]] = [ $"https://{sourceId}.example/{categoryKeys[0]}" ]
                },
                SitemapHints = [ $"https://{sourceId}.example/sitemap.xml" ],
                AllowedPathPrefixes = [ $"/{categoryKeys[0]}", "/product" ],
                ExcludedPathPrefixes = [ "/support" ],
                ProductUrlPatterns = [ "/product/" ],
                ListingUrlPatterns = [ "/category/" ],
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
                Status = readinessStatus,
                AssignedCategoryCount = categoryKeys.Count,
                CrawlableCategoryCount = readinessStatus == "Blocked" ? 0 : categoryKeys.Count,
                Summary = readinessStatus == "Blocked"
                    ? "Assigned categories are planned or disabled for crawling."
                    : $"All {categoryKeys.Count} assigned categories are crawl-ready."
            },
            Health = new SourceHealthSummaryDto
            {
                Status = healthStatus,
                TrustScore = healthStatus == "Healthy" ? 91m : 48m,
                CoveragePercent = healthStatus == "Healthy" ? 87m : 42m,
                SuccessfulCrawlRate = healthStatus == "Healthy" ? 93m : 55m,
                SnapshotUtc = new DateTime(2026, 03, 23, 08, 00, 00, DateTimeKind.Utc)
            },
            LastActivity = new SourceLastActivityDto
            {
                TimestampUtc = new DateTime(2026, 03, 23, 09, 00, 00, DateTimeKind.Utc),
                Status = healthStatus == "Healthy" ? "succeeded" : "failed",
                DurationMs = 1830,
                ExtractedProductCount = 12,
                HadMeaningfulChange = healthStatus == "Healthy",
                MeaningfulChangeSummary = healthStatus == "Healthy" ? "Detected updated specifications." : null,
                ErrorMessage = healthStatus == "Healthy" ? null : "Timeout while fetching category page."
            },
            CreatedUtc = new DateTime(2026, 03, 20, 10, 00, 00, DateTimeKind.Utc),
            UpdatedUtc = new DateTime(2026, 03, 23, 09, 15, 00, DateTimeKind.Utc)
        };
    }
}