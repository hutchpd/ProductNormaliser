using System.Net;
using System.Text.RegularExpressions;
using ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Tests;

[TestFixture]
public sealed class MilestoneHappyPathWorkflowTests
{
    [Test]
    public async Task OperatorJourney_CanSelectCategoryLaunchCrawlAndViewActiveProgress()
    {
        var fakeAdminApiClient = CreateHappyPathClient();

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var categoryResponse = await client.GetAsync("/Categories/Index?selectedCategory=tv");
        categoryResponse.EnsureSuccessStatusCode();
        var categoryHtml = await categoryResponse.Content.ReadAsStringAsync();

        var crawlJobsHtml = await client.GetStringAsync("/CrawlJobs/Index");
        var requestVerificationToken = ExtractRequestVerificationToken(crawlJobsHtml);

        var launchResponse = await client.PostAsync("/CrawlJobs/Index?handler=Launch", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", requestVerificationToken),
            new KeyValuePair<string, string>("Launch.RequestType", "category"),
            new KeyValuePair<string, string>("Launch.SelectedCategoryKeys", "tv"),
            new KeyValuePair<string, string>("Launch.SelectedSourceIds", "ao_uk")
        }));

        Assert.That(launchResponse.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(launchResponse.Headers.Location, Is.Not.Null);

        var detailsUrl = launchResponse.Headers.Location!.ToString();
        var detailsHtml = await client.GetStringAsync(detailsUrl);

        Assert.Multiple(() =>
        {
            Assert.That(categoryHtml, Does.Contain("data-selected-chip>TVs<"));
            Assert.That(crawlJobsHtml, Does.Contain("Create a targeted job"));
            Assert.That(crawlJobsHtml, Does.Contain("data-selected-chip>TVs<"));
            Assert.That(fakeAdminApiClient.LastCreatedJobRequest, Is.Not.Null);
            Assert.That(fakeAdminApiClient.LastCreatedJobRequest!.RequestedCategories, Is.EqualTo(new[] { "tv" }));
            Assert.That(fakeAdminApiClient.LastCreatedJobRequest.RequestedSources, Is.EqualTo(new[] { "ao_uk" }));
            Assert.That(detailsUrl, Does.Contain("/CrawlJobs/Details"));
            Assert.That(detailsUrl, Does.Contain("jobId=job_tv_20260323"));
            Assert.That(detailsUrl, Does.Contain("selectedCategory=tv"));
            Assert.That(fakeAdminApiClient.LastRequestedCrawlJobId, Is.EqualTo("job_tv_20260323"));
            Assert.That(detailsHtml, Does.Contain("This job is active."));
            Assert.That(detailsHtml, Does.Contain("job_tv_20260323"));
            Assert.That(detailsHtml, Does.Contain("Running"));
            Assert.That(detailsHtml, Does.Contain("4 of 10 targets processed"));
            Assert.That(detailsHtml, Does.Contain("tv"));
            Assert.That(detailsHtml, Does.Contain("ao_uk"));
        });
    }

    [Test]
    public async Task OperatorJourney_CanOpenProductsProductDetailAndQualityDashboardForSelectedCategory()
    {
        var fakeAdminApiClient = CreateHappyPathClient();

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var seedResponse = await client.GetAsync("/Categories/Index?selectedCategory=tv");
        seedResponse.EnsureSuccessStatusCode();

        var productsHtml = await client.GetStringAsync("/Products/Index");
        var productDetailHtml = await client.GetStringAsync("/Products/Details?productId=canon-tv-1&category=tv&returnPage=1");
        var qualityHtml = await client.GetStringAsync("/Quality/Index");

        Assert.Multiple(() =>
        {
            Assert.That(fakeAdminApiClient.LastProductQuery, Is.Not.Null);
            Assert.That(fakeAdminApiClient.LastProductQuery!.CategoryKey, Is.EqualTo("tv"));
            Assert.That(productsHtml, Does.Contain("Analysis-ready product explorer"));
            Assert.That(productsHtml, Does.Contain("Sony Bravia XR 55 A80L"));
            Assert.That(productsHtml, Does.Contain("/Products/Details?productId=canon-tv-1&amp;category=tv"));

            Assert.That(fakeAdminApiClient.LastRequestedProductId, Is.EqualTo("canon-tv-1"));
            Assert.That(fakeAdminApiClient.LastRequestedProductHistoryId, Is.EqualTo("canon-tv-1"));
            Assert.That(productDetailHtml, Does.Contain("Sony Bravia XR 55 A80L"));
            Assert.That(productDetailHtml, Does.Contain("Back to explorer"));
            Assert.That(productDetailHtml, Does.Contain("AO UK"));
            Assert.That(productDetailHtml, Does.Contain("Currys"));
            Assert.That(productDetailHtml, Does.Contain("screen_size"));

            Assert.That(fakeAdminApiClient.LastCoverageCategoryKey, Is.EqualTo("tv"));
            Assert.That(fakeAdminApiClient.LastUnmappedCategoryKey, Is.EqualTo("tv"));
            Assert.That(fakeAdminApiClient.LastAttributeStabilityCategoryKey, Is.EqualTo("tv"));
            Assert.That(fakeAdminApiClient.LastSourceDisagreementsCategoryKey, Is.EqualTo("tv"));
            Assert.That(qualityHtml, Does.Contain("TVs quality dashboard"));
            Assert.That(qualityHtml, Does.Contain("Schema coverage and reliability"));
            Assert.That(qualityHtml, Does.Contain("Refresh Rate"));
            Assert.That(qualityHtml, Does.Contain("remote_control_type"));
            Assert.That(qualityHtml, Does.Contain("panel_type"));
        });
    }

    private static FakeAdminApiClient CreateHappyPathClient()
    {
        var activeJob = new CrawlJobDto
        {
            JobId = "job_tv_20260323",
            RequestType = "category",
            RequestedCategories = ["tv"],
            RequestedSources = ["ao_uk"],
            TotalTargets = 10,
            ProcessedTargets = 4,
            SuccessCount = 3,
            SkippedCount = 1,
            FailedCount = 0,
            CancelledCount = 0,
            StartedAt = new DateTime(2026, 03, 23, 10, 00, 00, DateTimeKind.Utc),
            LastUpdatedAt = new DateTime(2026, 03, 23, 10, 08, 00, DateTimeKind.Utc),
            EstimatedCompletion = new DateTime(2026, 03, 23, 10, 20, 00, DateTimeKind.Utc),
            Status = "running",
            PerCategoryBreakdown =
            [
                new CrawlJobCategoryBreakdownDto
                {
                    CategoryKey = "tv",
                    TotalTargets = 10,
                    ProcessedTargets = 4,
                    SuccessCount = 3,
                    SkippedCount = 1,
                    FailedCount = 0,
                    CancelledCount = 0
                }
            ]
        };

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
                    CrawlSupportStatus = "Supported",
                    SchemaCompletenessScore = 0.92m,
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
                    Description = "Primary TV source",
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
                    CreatedUtc = new DateTime(2026, 03, 20, 10, 00, 00, DateTimeKind.Utc),
                    UpdatedUtc = new DateTime(2026, 03, 23, 09, 00, 00, DateTimeKind.Utc)
                }
            ],
            CreatedJob = new CrawlJobDto
            {
                JobId = activeJob.JobId,
                RequestType = "category",
                RequestedCategories = ["tv"],
                RequestedSources = ["ao_uk"],
                Status = "pending"
            },
            CrawlJob = activeJob,
            CrawlJobsPage = new CrawlJobListResponseDto
            {
                Items = [activeJob],
                Page = 1,
                PageSize = 30,
                TotalCount = 1,
                TotalPages = 1
            },
            ProductPage = new ProductListResponseDto
            {
                Items =
                [
                    new ProductSummaryDto
                    {
                        Id = "canon-tv-1",
                        CategoryKey = "tv",
                        Brand = "Sony",
                        ModelNumber = "XR-55A80L",
                        Gtin = "1234567890123",
                        DisplayName = "Sony Bravia XR 55 A80L",
                        SourceCount = 2,
                        AttributeCount = 12,
                        EvidenceCount = 7,
                        ConflictAttributeCount = 1,
                        HasConflict = true,
                        CompletenessScore = 0.88m,
                        CompletenessStatus = "complete",
                        PopulatedKeyAttributeCount = 7,
                        ExpectedKeyAttributeCount = 8,
                        FreshnessStatus = "fresh",
                        FreshnessAgeDays = 2,
                        KeyAttributes =
                        [
                            new ProductKeyAttributeDto
                            {
                                AttributeKey = "screen_size",
                                DisplayName = "Screen Size",
                                Value = "55 in",
                                HasConflict = true,
                                Confidence = 0.92m
                            }
                        ],
                        UpdatedUtc = new DateTime(2026, 03, 23, 08, 00, 00, DateTimeKind.Utc)
                    }
                ],
                Page = 1,
                PageSize = 12,
                TotalCount = 1,
                TotalPages = 1
            },
            Product = new ProductDetailDto
            {
                Id = "canon-tv-1",
                CategoryKey = "tv",
                Brand = "Sony",
                ModelNumber = "XR-55A80L",
                Gtin = "1234567890123",
                DisplayName = "Sony Bravia XR 55 A80L",
                CreatedUtc = new DateTime(2026, 03, 20, 08, 00, 00, DateTimeKind.Utc),
                UpdatedUtc = new DateTime(2026, 03, 23, 08, 00, 00, DateTimeKind.Utc),
                SourceCount = 2,
                EvidenceCount = 7,
                ConflictAttributeCount = 1,
                HasConflict = true,
                CompletenessScore = 0.88m,
                CompletenessStatus = "complete",
                PopulatedKeyAttributeCount = 7,
                ExpectedKeyAttributeCount = 8,
                FreshnessStatus = "fresh",
                FreshnessAgeDays = 2,
                KeyAttributes =
                [
                    new ProductKeyAttributeDto
                    {
                        AttributeKey = "screen_size",
                        DisplayName = "Screen Size",
                        Value = "55 in",
                        HasConflict = true,
                        Confidence = 0.92m
                    }
                ],
                Attributes =
                [
                    new ProductAttributeDetailDto
                    {
                        AttributeKey = "screen_size",
                        Value = "55",
                        ValueType = "number",
                        Unit = "in",
                        Confidence = 0.92m,
                        HasConflict = true,
                        Evidence =
                        [
                            new AttributeEvidenceDto
                            {
                                SourceName = "AO UK",
                                SourceUrl = "https://ao.example/p/ao-1",
                                SourceProductId = "ao-1",
                                SourceAttributeKey = "screen_size",
                                RawValue = "55",
                                SelectorOrPath = "specs.screenSize",
                                Confidence = 0.94m,
                                ObservedUtc = new DateTime(2026, 03, 22, 10, 01, 00, DateTimeKind.Utc)
                            },
                            new AttributeEvidenceDto
                            {
                                SourceName = "Currys",
                                SourceUrl = "https://currys.example/p/c-1",
                                SourceProductId = "currys-1",
                                SourceAttributeKey = "screen_size",
                                RawValue = "54.6",
                                SelectorOrPath = "specifications.screenSize",
                                Confidence = 0.89m,
                                ObservedUtc = new DateTime(2026, 03, 22, 10, 02, 00, DateTimeKind.Utc)
                            }
                        ]
                    }
                ],
                SourceProducts =
                [
                    new SourceProductDetailDto
                    {
                        Id = "ao-1",
                        SourceName = "AO UK",
                        SourceUrl = "https://ao.example/p/ao-1",
                        ModelNumber = "XR-55A80L",
                        Gtin = "1234567890123",
                        Title = "Sony Bravia XR 55 OLED",
                        RawSchemaJson = "{}",
                        RawAttributes =
                        [
                            new SourceAttributeValueDto { AttributeKey = "panel_type", Value = "OLED", ValueType = "string", SourcePath = "specs.panelType" },
                            new SourceAttributeValueDto { AttributeKey = "screen_size", Value = "55", ValueType = "number", Unit = "in", SourcePath = "specs.screenSize" }
                        ]
                    },
                    new SourceProductDetailDto
                    {
                        Id = "currys-1",
                        SourceName = "Currys",
                        SourceUrl = "https://currys.example/p/c-1",
                        ModelNumber = "XR55A80L",
                        Gtin = "1234567890123",
                        Title = "Sony 55 inch OLED TV",
                        RawSchemaJson = "{}",
                        RawAttributes =
                        [
                            new SourceAttributeValueDto { AttributeKey = "screen_size", Value = "54.6", ValueType = "number", Unit = "in", SourcePath = "specifications.screenSize" }
                        ]
                    }
                ]
            },
            ProductHistory =
            [
                new ProductChangeEventDto
                {
                    CanonicalProductId = "canon-tv-1",
                    CategoryKey = "tv",
                    AttributeKey = "screen_size",
                    OldValue = "54.6",
                    NewValue = "55",
                    SourceName = "AO UK",
                    TimestampUtc = new DateTime(2026, 03, 23, 07, 45, 00, DateTimeKind.Utc)
                }
            ],
            DetailedCoverage = new DetailedCoverageResponseDto
            {
                CategoryKey = "tv",
                TotalCanonicalProducts = 42,
                TotalSourceProducts = 97,
                Attributes =
                [
                    new AttributeCoverageDetailDto
                    {
                        AttributeKey = "refresh_rate_hz",
                        DisplayName = "Refresh Rate",
                        PresentProductCount = 30,
                        MissingProductCount = 12,
                        CoveragePercent = 71m,
                        ConflictProductCount = 4,
                        ConflictPercent = 9m,
                        AverageConfidence = 84m,
                        AgreementPercent = 76m,
                        ReliabilityScore = 79m
                    }
                ],
                MostMissingAttributes =
                [
                    new AttributeGapDto
                    {
                        AttributeKey = "remote_control_type",
                        DisplayName = "Remote Control Type",
                        ProductCount = 14,
                        Percentage = 33m
                    }
                ],
                MostConflictedAttributes =
                [
                    new AttributeGapDto
                    {
                        AttributeKey = "screen_size",
                        DisplayName = "Screen Size",
                        ProductCount = 4,
                        Percentage = 9m
                    }
                ]
            },
            UnmappedAttributes =
            [
                new UnmappedAttributeDto
                {
                    CanonicalKey = "remote_control_type",
                    RawAttributeKey = "remote_control_type",
                    OccurrenceCount = 8,
                    SourceNames = ["AO UK"],
                    SampleValues = ["Smart remote"],
                    LastSeenUtc = new DateTime(2026, 03, 22, 09, 00, 00, DateTimeKind.Utc)
                }
            ],
            SourceDisagreements =
            [
                new SourceAttributeDisagreementDto
                {
                    SourceName = "AO UK",
                    CategoryKey = "tv",
                    AttributeKey = "panel_type",
                    TotalComparisons = 12,
                    TimesDisagreed = 3,
                    TimesWon = 8,
                    DisagreementRate = 25m,
                    WinRate = 67m,
                    LastUpdatedUtc = new DateTime(2026, 03, 23, 08, 30, 00, DateTimeKind.Utc)
                }
            ],
            AttributeStability =
            [
                new AttributeStabilityDto
                {
                    CategoryKey = "tv",
                    AttributeKey = "panel_type",
                    ChangeCount = 2,
                    OscillationCount = 1,
                    DistinctValueCount = 2,
                    StabilityScore = 82m,
                    IsSuspicious = false
                }
            ]
        };
    }

    private static string ExtractRequestVerificationToken(string html)
    {
        var match = Regex.Match(
            html,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (!match.Success)
        {
            Assert.Fail("Expected crawl jobs page to render an antiforgery token.");
        }

        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}