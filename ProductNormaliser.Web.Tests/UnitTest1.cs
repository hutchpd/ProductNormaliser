using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Tests;

public sealed class AdminApiClientTests
{
    [Test]
    public async Task GetSourcesAsync_DeserialisesSourceList()
    {
        var client = CreateClient(HttpStatusCode.OK, new[]
        {
            new SourceDto
            {
                SourceId = "ao_uk",
                DisplayName = "AO UK",
                BaseUrl = "https://ao.com/",
                Host = "ao.com",
                Description = "Appliances",
                IsEnabled = true,
                SupportedCategoryKeys = ["tv", "refrigerator"],
                ThrottlingPolicy = new SourceThrottlingPolicyDto
                {
                    MinDelayMs = 1000,
                    MaxDelayMs = 4000,
                    MaxConcurrentRequests = 2,
                    RequestsPerMinute = 24,
                    RespectRobotsTxt = true
                },
                CreatedUtc = new DateTime(2026, 03, 20, 10, 00, 00, DateTimeKind.Utc),
                UpdatedUtc = new DateTime(2026, 03, 20, 10, 10, 00, DateTimeKind.Utc)
            }
        });

        var sources = await client.GetSourcesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(sources, Has.Count.EqualTo(1));
            Assert.That(sources[0].DisplayName, Is.EqualTo("AO UK"));
            Assert.That(sources[0].SupportedCategoryKeys, Does.Contain("refrigerator"));
        });
    }

    [Test]
    public async Task GetCategoryDetailAsync_ReturnsNullForNotFound()
    {
        var client = CreateClient(HttpStatusCode.NotFound, payload: null);

        var category = await client.GetCategoryDetailAsync("unknown-category");

        Assert.That(category, Is.Null);
    }

    [Test]
    public void RegisterSourceAsync_ThrowsValidationExceptionForProblemResponse()
    {
        var validation = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["supportedCategoryKeys"] = ["Unknown category keys: smartwatch."]
        })
        {
            Status = 400,
            Title = "One or more validation errors occurred."
        };

        var client = CreateClient(HttpStatusCode.BadRequest, validation, "application/problem+json");

        var action = async () => await client.RegisterSourceAsync(new RegisterSourceRequest
        {
            SourceId = "alpha",
            DisplayName = "Alpha",
            BaseUrl = "https://alpha.example/",
            SupportedCategoryKeys = ["smartwatch"]
        });

        Assert.That(action, Throws.TypeOf<AdminApiValidationException>());
    }

        [Test]
        public void GetDetailedCoverageAsync_ThrowsWhenResponseBodyIsEmpty()
        {
                var client = CreateClient(HttpStatusCode.OK, payload: null);

                var action = async () => await client.GetDetailedCoverageAsync("monitor");

                Assert.That(action, Throws.TypeOf<AdminApiException>()
                        .With.Message.Contains("response body was empty"));
        }

        [Test]
        public void GetCategoriesAsync_ThrowsWhenCrawlSupportStatusIsUnsupported()
        {
                const string payload = """
                        [
                            {
                                "categoryKey": "tv",
                                "displayName": "TVs",
                                "familyKey": "display",
                                "familyDisplayName": "Display",
                                "iconKey": "tv",
                                "crawlSupportStatus": "Retired",
                                "schemaCompletenessScore": 1.0,
                                "isEnabled": true
                            }
                        ]
                        """;

                var client = CreateRawClient(HttpStatusCode.OK, payload);

                var action = async () => await client.GetCategoriesAsync();

                Assert.That(action, Throws.TypeOf<AdminApiException>()
                        .With.Message.Contains("crawlSupportStatus"));
        }

        [Test]
        public void GetSourcesAsync_ThrowsWhenRequiredFieldsAreMissing()
        {
                const string payload = """
                        [
                            {
                                "displayName": "AO UK",
                                "baseUrl": "https://ao.com/",
                                "host": "ao.com",
                                "isEnabled": true,
                                "supportedCategoryKeys": ["tv"],
                                "throttlingPolicy": {
                                    "minDelayMs": 1000,
                                    "maxDelayMs": 4000,
                                    "maxConcurrentRequests": 2,
                                    "requestsPerMinute": 24,
                                    "respectRobotsTxt": true
                                },
                                "createdUtc": "2026-03-20T10:00:00Z",
                                "updatedUtc": "2026-03-20T10:10:00Z"
                            }
                        ]
                        """;

                var client = CreateRawClient(HttpStatusCode.OK, payload);

                var action = async () => await client.GetSourcesAsync();

                Assert.That(action, Throws.TypeOf<AdminApiException>()
                        .With.Message.Contains("sourceId"));
        }

        [Test]
        public void GetCrawlJobsAsync_ThrowsWhenJobStatusIsUnsupported()
        {
                const string payload = """
                        {
                            "items": [
                                {
                                    "jobId": "job_1",
                                    "requestType": "category",
                                    "requestedCategories": ["tv"],
                                    "requestedSources": [],
                                    "requestedProductIds": [],
                                    "totalTargets": 10,
                                    "processedTargets": 4,
                                    "successCount": 4,
                                    "skippedCount": 0,
                                    "failedCount": 0,
                                    "cancelledCount": 0,
                                    "startedAt": "2026-03-20T10:00:00Z",
                                    "lastUpdatedAt": "2026-03-20T10:05:00Z",
                                    "estimatedCompletion": "2026-03-20T10:15:00Z",
                                    "status": "paused",
                                    "perCategoryBreakdown": []
                                }
                            ],
                            "page": 1,
                            "pageSize": 10,
                            "totalCount": 1,
                            "totalPages": 1
                        }
                        """;

                var client = CreateRawClient(HttpStatusCode.OK, payload);

                var action = async () => await client.GetCrawlJobsAsync();

                Assert.That(action, Throws.TypeOf<AdminApiException>()
                        .With.Message.Contains("crawlJobs.items[0].status"));
        }

        [Test]
        public void GetProductAsync_ThrowsWhenProductStatusesAreUnsupported()
        {
                const string payload = """
                        {
                            "id": "canon-1",
                            "categoryKey": "tv",
                            "brand": "Sony",
                            "displayName": "Sony Bravia XR",
                            "createdUtc": "2026-03-20T10:00:00Z",
                            "updatedUtc": "2026-03-20T10:10:00Z",
                            "sourceCount": 3,
                            "evidenceCount": 8,
                            "conflictAttributeCount": 2,
                            "hasConflict": true,
                            "completenessScore": 0.75,
                            "completenessStatus": "unknown",
                            "populatedKeyAttributeCount": 6,
                            "expectedKeyAttributeCount": 8,
                            "freshnessStatus": "decaying",
                            "freshnessAgeDays": 40,
                            "keyAttributes": [],
                            "attributes": [],
                            "sourceProducts": []
                        }
                        """;

                var client = CreateRawClient(HttpStatusCode.OK, payload);

                var action = async () => await client.GetProductAsync("canon-1");

                Assert.That(action, Throws.TypeOf<AdminApiException>()
                        .With.Message.Contains("completenessStatus"));
        }

        [Test]
        public async Task GetDetailedCoverageAsync_ToleratesUnknownFields()
        {
                const string payload = """
                        {
                            "categoryKey": "monitor",
                            "totalCanonicalProducts": 12,
                            "totalSourceProducts": 18,
                            "attributes": [
                                {
                                    "attributeKey": "refresh_rate_hz",
                                    "displayName": "Refresh Rate",
                                    "presentProductCount": 8,
                                    "missingProductCount": 4,
                                    "coveragePercent": 62,
                                    "conflictProductCount": 1,
                                    "conflictPercent": 8,
                                    "averageConfidence": 77,
                                    "agreementPercent": 71,
                                    "reliabilityScore": 58,
                                    "futureMetric": 99
                                }
                            ],
                            "mostMissingAttributes": [],
                            "mostConflictedAttributes": [],
                            "futureSection": {
                                "enabled": true
                            }
                        }
                        """;

                var client = CreateRawClient(HttpStatusCode.OK, payload);

                var result = await client.GetDetailedCoverageAsync("monitor");

                Assert.Multiple(() =>
                {
                        Assert.That(result.CategoryKey, Is.EqualTo("monitor"));
                        Assert.That(result.Attributes, Has.Count.EqualTo(1));
                        Assert.That(result.Attributes[0].AttributeKey, Is.EqualTo("refresh_rate_hz"));
                });
        }

        [Test]
        public void GetProductsAsync_ThrowsAdminApiExceptionForServerErrors()
        {
                var client = CreateRawClient(HttpStatusCode.InternalServerError, "server exploded", "text/plain");

                var action = async () => await client.GetProductsAsync();

                Assert.That(action, Throws.TypeOf<AdminApiException>()
                        .With.Message.Contains("500")
                        .And.Message.Contains("server exploded"));
        }

    [Test]
    public async Task GetCrawlJobsAsync_DeserialisesPagedResponseAndPreservesQueryParameters()
    {
        var requestUri = string.Empty;
        var client = CreateClient((request, _) =>
        {
            requestUri = request.RequestUri!.ToString();
            return Task.FromResult(CreateJsonResponse(HttpStatusCode.OK, new CrawlJobListResponseDto
            {
                Items =
                [
                    new CrawlJobDto
                    {
                        JobId = "job_1",
                        RequestType = "category",
                        RequestedCategories = ["tv"],
                        TotalTargets = 10,
                        ProcessedTargets = 4,
                        SuccessCount = 3,
                        SkippedCount = 1,
                        FailedCount = 0,
                        StartedAt = new DateTime(2026, 03, 20, 10, 00, 00, DateTimeKind.Utc),
                        LastUpdatedAt = new DateTime(2026, 03, 20, 10, 05, 00, DateTimeKind.Utc),
                        EstimatedCompletion = new DateTime(2026, 03, 20, 10, 15, 00, DateTimeKind.Utc),
                        Status = "running"
                    }
                ],
                Page = 2,
                PageSize = 5,
                TotalCount = 11,
                TotalPages = 3
            }));
        });

        var jobs = await client.GetCrawlJobsAsync(new CrawlJobQueryDto
        {
            Status = "running",
            CategoryKey = "tv",
            Page = 2,
            PageSize = 5
        });

        Assert.Multiple(() =>
        {
            Assert.That(requestUri, Does.Contain("status=running"));
            Assert.That(requestUri, Does.Contain("category=tv"));
            Assert.That(requestUri, Does.Contain("page=2"));
            Assert.That(jobs.Items, Has.Count.EqualTo(1));
            Assert.That(jobs.TotalPages, Is.EqualTo(3));
            Assert.That(jobs.Items[0].PerCategoryBreakdown, Is.Empty);
        });
    }

    [Test]
    public async Task GetProductsAsync_DeserialisesProductPageAndPreservesExplorerFilters()
    {
        var requestUri = string.Empty;
        var client = CreateClient((request, _) =>
        {
            requestUri = request.RequestUri!.ToString();
            return Task.FromResult(CreateJsonResponse(HttpStatusCode.OK, new ProductListResponseDto
            {
                Items =
                [
                    new ProductSummaryDto
                    {
                        Id = "canon-1",
                        CategoryKey = "tv",
                        Brand = "Sony",
                        ModelNumber = "XR-55A80L",
                        DisplayName = "Sony Bravia XR",
                        SourceCount = 3,
                        EvidenceCount = 8,
                        AttributeCount = 12,
                        HasConflict = true,
                        ConflictAttributeCount = 2,
                        CompletenessScore = 0.75m,
                        CompletenessStatus = "partial",
                        FreshnessStatus = "stale",
                        FreshnessAgeDays = 40,
                        UpdatedUtc = new DateTime(2026, 03, 20, 10, 10, 00, DateTimeKind.Utc)
                    }
                ],
                Page = 1,
                PageSize = 12,
                TotalCount = 1,
                TotalPages = 1
            }));
        });

        var products = await client.GetProductsAsync(new ProductListQueryDto
        {
            CategoryKey = "tv",
            Search = "sony",
            MinSourceCount = 3,
            Freshness = "stale",
            ConflictStatus = "with_conflicts",
            CompletenessStatus = "partial"
        });

        Assert.Multiple(() =>
        {
            Assert.That(requestUri, Does.Contain("category=tv"));
            Assert.That(requestUri, Does.Contain("search=sony"));
            Assert.That(requestUri, Does.Contain("minSourceCount=3"));
            Assert.That(requestUri, Does.Contain("freshness=stale"));
            Assert.That(requestUri, Does.Contain("conflictStatus=with_conflicts"));
            Assert.That(requestUri, Does.Contain("completeness=partial"));
            Assert.That(products.Items, Has.Count.EqualTo(1));
            Assert.That(products.Items[0].HasConflict, Is.True);
            Assert.That(products.Items[0].CompletenessStatus, Is.EqualTo("partial"));
        });
    }

    [Test]
    public async Task CreateCrawlJobAsync_PostsToCreateEndpoint()
    {
        var requestUri = string.Empty;
        CreateCrawlJobRequest? requestPayload = null;
        var client = CreateClient(async (request, cancellationToken) =>
        {
            requestUri = request.RequestUri!.ToString();
            requestPayload = await request.Content!.ReadFromJsonAsync<CreateCrawlJobRequest>(cancellationToken: cancellationToken);
            return CreateJsonResponse(HttpStatusCode.Created, new CrawlJobDto { JobId = "job_22", RequestType = "category", Status = "pending" });
        });

        var result = await client.CreateCrawlJobAsync(new CreateCrawlJobRequest
        {
            RequestType = "category",
            RequestedCategories = ["tv", "monitor"],
            RequestedSources = ["ao_uk"]
        });

        Assert.Multiple(() =>
        {
            Assert.That(requestUri, Does.Contain("api/crawl/jobs"));
            Assert.That(requestPayload, Is.Not.Null);
            Assert.That(requestPayload!.RequestedCategories, Is.EqualTo(new[] { "tv", "monitor" }));
            Assert.That(requestPayload.RequestedSources, Is.EqualTo(new[] { "ao_uk" }));
            Assert.That(result.JobId, Is.EqualTo("job_22"));
        });
    }

    [Test]
    public async Task CancelCrawlJobAsync_PostsToCancelEndpoint()
    {
        var requestUri = string.Empty;
        var client = CreateClient((request, _) =>
        {
            requestUri = request.RequestUri!.ToString();
            return Task.FromResult(CreateJsonResponse(HttpStatusCode.OK, new CrawlJobDto { JobId = "job_1", RequestType = "category", Status = "cancel_requested" }));
        });

        var result = await client.CancelCrawlJobAsync("job_1");

        Assert.Multiple(() =>
        {
            Assert.That(requestUri, Does.Contain("api/crawl/jobs/job_1/cancel"));
            Assert.That(result.Status, Is.EqualTo("cancel_requested"));
        });
    }

    [Test]
    public async Task GetDetailedCoverageAsync_DeserialisesCoverageResponseAndPreservesCategory()
    {
        var requestUri = string.Empty;
        var client = CreateClient((request, _) =>
        {
            requestUri = request.RequestUri!.ToString();
            return Task.FromResult(CreateJsonResponse(HttpStatusCode.OK, new DetailedCoverageResponseDto
            {
                CategoryKey = "monitor",
                TotalCanonicalProducts = 12,
                Attributes =
                [
                    new AttributeCoverageDetailDto
                    {
                        AttributeKey = "refresh_rate_hz",
                        DisplayName = "Refresh Rate",
                        CoveragePercent = 62m,
                        ReliabilityScore = 58m
                    }
                ]
            }));
        });

        var result = await client.GetDetailedCoverageAsync("monitor");

        Assert.Multiple(() =>
        {
            Assert.That(requestUri, Does.Contain("api/quality/coverage/detailed"));
            Assert.That(requestUri, Does.Contain("categoryKey=monitor"));
            Assert.That(result.CategoryKey, Is.EqualTo("monitor"));
            Assert.That(result.Attributes, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task GetSourceHistoryAsync_PreservesCategoryAndSourceQuery()
    {
        var requestUri = string.Empty;
        var client = CreateClient((request, _) =>
        {
            requestUri = request.RequestUri!.ToString();
            return Task.FromResult(CreateJsonResponse(HttpStatusCode.OK, new[]
            {
                new SourceQualitySnapshotDto
                {
                    SourceName = "Northwind",
                    CategoryKey = "monitor",
                    TimestampUtc = new DateTime(2026, 03, 23, 09, 00, 00, DateTimeKind.Utc),
                    AttributeCoverage = 84m,
                    HistoricalTrustScore = 81m
                }
            }));
        });

        var result = await client.GetSourceHistoryAsync("monitor", "Northwind");

        Assert.Multiple(() =>
        {
            Assert.That(requestUri, Does.Contain("api/quality/source-history"));
            Assert.That(requestUri, Does.Contain("categoryKey=monitor"));
            Assert.That(requestUri, Does.Contain("sourceName=Northwind"));
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].SourceName, Is.EqualTo("Northwind"));
        });
    }

    private static ProductNormaliserAdminApiClient CreateClient(HttpStatusCode statusCode, object? payload, string mediaType = "application/json")
    {
        return CreateClient((_, _) => Task.FromResult(CreateJsonResponse(statusCode, payload, mediaType)));
    }

    private static ProductNormaliserAdminApiClient CreateRawClient(HttpStatusCode statusCode, string payload, string mediaType = "application/json")
    {
        return CreateClient((_, _) => Task.FromResult(CreateRawResponse(statusCode, payload, mediaType)));
    }

    private static ProductNormaliserAdminApiClient CreateClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        return new ProductNormaliserAdminApiClient(new HttpClient(new StubHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("http://localhost:5209/")
        });
    }

    private static HttpResponseMessage CreateJsonResponse(HttpStatusCode statusCode, object? payload, string mediaType = "application/json")
    {
        var response = new HttpResponseMessage(statusCode);
        if (payload is not null)
        {
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            response.Content = new StringContent(json, Encoding.UTF8, mediaType);
        }

        return response;
    }

    private static HttpResponseMessage CreateRawResponse(HttpStatusCode statusCode, string payload, string mediaType = "application/json")
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(payload, Encoding.UTF8, mediaType)
        };
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return handler(request, cancellationToken);
        }
    }

}
