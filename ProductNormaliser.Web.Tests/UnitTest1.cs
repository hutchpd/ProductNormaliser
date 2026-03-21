using System.Net;
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
    public async Task GetProductsAsync_DeserialisesProductPage()
    {
        var client = CreateClient(HttpStatusCode.OK, new ProductListResponseDto
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
                    AttributeCount = 12,
                    UpdatedUtc = new DateTime(2026, 03, 20, 10, 10, 00, DateTimeKind.Utc)
                }
            ],
            Page = 1,
            PageSize = 12,
            TotalCount = 1,
            TotalPages = 1
        });

        var products = await client.GetProductsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(products.Items, Has.Count.EqualTo(1));
            Assert.That(products.Items[0].Brand, Is.EqualTo("Sony"));
            Assert.That(products.TotalCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task CancelCrawlJobAsync_PostsToCancelEndpoint()
    {
        var requestUri = string.Empty;
        var client = CreateClient((request, _) =>
        {
            requestUri = request.RequestUri!.ToString();
            return Task.FromResult(CreateJsonResponse(HttpStatusCode.OK, new CrawlJobDto { JobId = "job_1", Status = "cancel_requested" }));
        });

        var result = await client.CancelCrawlJobAsync("job_1");

        Assert.Multiple(() =>
        {
            Assert.That(requestUri, Does.Contain("api/crawl/jobs/job_1/cancel"));
            Assert.That(result.Status, Is.EqualTo("cancel_requested"));
        });
    }

    [Test]
    public async Task CrawlJobsPage_OnPostLaunchAsync_CreatesJobAndRedirects()
    {
        var client = new FakeAdminApiClient
        {
            CreatedJob = new CrawlJobDto { JobId = "job_123", Status = "pending" }
        };
        var model = new ProductNormaliser.Web.Pages.CrawlJobs.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.CrawlJobs.IndexModel>.Instance)
        {
            Launch = new ProductNormaliser.Web.Pages.CrawlJobs.IndexModel.LaunchCrawlJobInput
            {
                RequestType = "category",
                SelectedCategoryKeys = ["tv"]
            }
        };

        var result = await model.OnPostLaunchAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(client.LastCreatedJobRequest, Is.Not.Null);
            Assert.That(client.LastCreatedJobRequest!.RequestedCategories, Is.EqualTo(new[] { "tv" }));
            Assert.That(result, Is.TypeOf<RedirectToPageResult>());
            Assert.That(((RedirectToPageResult)result).RouteValues!["jobId"], Is.EqualTo("job_123"));
        });
    }

    [Test]
    public async Task CrawlJobsPage_OnPostCancelAsync_CancelsJobAndRedirects()
    {
        var client = new FakeAdminApiClient
        {
            CancelledJob = new CrawlJobDto { JobId = "job_123", Status = "cancel_requested" }
        };
        var model = new ProductNormaliser.Web.Pages.CrawlJobs.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.CrawlJobs.IndexModel>.Instance)
        {
            Launch = new ProductNormaliser.Web.Pages.CrawlJobs.IndexModel.LaunchCrawlJobInput
            {
                RequestType = "category",
                SelectedCategoryKeys = ["tv", "monitor"]
            },
            PageNumber = 2
        };

        var result = await model.OnPostCancelAsync("job_123", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(client.LastCancelledJobId, Is.EqualTo("job_123"));
            Assert.That(result, Is.TypeOf<RedirectToPageResult>());
            Assert.That(((RedirectToPageResult)result).RouteValues!["jobId"], Is.EqualTo("job_123"));
        });
    }

    [Test]
    public async Task ProductsPage_OnGetAsync_LoadsSelectedProductAndHistory()
    {
        var client = new FakeAdminApiClient
        {
            Categories = [new CategoryMetadataDto { CategoryKey = "tv", DisplayName = "TV", IsEnabled = true }],
            ProductPage = new ProductListResponseDto
            {
                Items = [new ProductSummaryDto { Id = "canon-1", CategoryKey = "tv", Brand = "Sony", DisplayName = "Sony Bravia" }],
                Page = 1,
                PageSize = 12,
                TotalCount = 1,
                TotalPages = 1
            },
            Product = new ProductDetailDto
            {
                Id = "canon-1",
                CategoryKey = "tv",
                Brand = "Sony",
                DisplayName = "Sony Bravia"
            },
            ProductHistory = [new ProductChangeEventDto { CanonicalProductId = "canon-1", CategoryKey = "tv", AttributeKey = "screen_size", SourceName = "ao", TimestampUtc = DateTime.UtcNow }]
        };
        var model = new ProductNormaliser.Web.Pages.Products.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Products.IndexModel>.Instance)
        {
            SelectedProductId = "canon-1"
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(model.SelectedProduct, Is.Not.Null);
            Assert.That(model.SelectedProduct!.Id, Is.EqualTo("canon-1"));
            Assert.That(model.ProductHistory, Has.Count.EqualTo(1));
            Assert.That(model.Products.Items, Has.Count.EqualTo(1));
        });
    }

    private static ProductNormaliserAdminApiClient CreateClient(HttpStatusCode statusCode, object? payload, string mediaType = "application/json")
    {
        return CreateClient((_, _) => Task.FromResult(CreateJsonResponse(statusCode, payload, mediaType)));
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

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return handler(request, cancellationToken);
        }
    }

    private sealed class FakeAdminApiClient : IProductNormaliserAdminApiClient
    {
        public StatsDto Stats { get; set; } = new();
        public IReadOnlyList<CategoryMetadataDto> Categories { get; set; } = [];
        public IReadOnlyList<CategoryFamilyDto> CategoryFamilies { get; set; } = [];
        public IReadOnlyList<CategoryMetadataDto> EnabledCategories { get; set; } = [];
        public CategoryDetailDto? CategoryDetail { get; set; }
        public IReadOnlyList<SourceDto> Sources { get; set; } = [];
        public SourceDto? Source { get; set; }
        public CrawlJobListResponseDto CrawlJobsPage { get; set; } = new();
        public CrawlJobDto? CrawlJob { get; set; }
        public CrawlJobDto? CreatedJob { get; set; }
        public CrawlJobDto? CancelledJob { get; set; }
        public CreateCrawlJobRequest? LastCreatedJobRequest { get; private set; }
        public string? LastCancelledJobId { get; private set; }
        public ProductListResponseDto ProductPage { get; set; } = new();
        public ProductDetailDto? Product { get; set; }
        public IReadOnlyList<ProductChangeEventDto> ProductHistory { get; set; } = [];

        public Task<StatsDto> GetStatsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Stats);
        public Task<IReadOnlyList<CategoryMetadataDto>> GetCategoriesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Categories);
        public Task<IReadOnlyList<CategoryFamilyDto>> GetCategoryFamiliesAsync(CancellationToken cancellationToken = default) => Task.FromResult(CategoryFamilies);
        public Task<IReadOnlyList<CategoryMetadataDto>> GetEnabledCategoriesAsync(CancellationToken cancellationToken = default) => Task.FromResult(EnabledCategories);
        public Task<CategoryDetailDto?> GetCategoryDetailAsync(string categoryKey, CancellationToken cancellationToken = default) => Task.FromResult(CategoryDetail);
        public Task<IReadOnlyList<SourceDto>> GetSourcesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Sources);
        public Task<SourceDto?> GetSourceAsync(string sourceId, CancellationToken cancellationToken = default) => Task.FromResult(Source);
        public Task<SourceDto> RegisterSourceAsync(RegisterSourceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SourceDto> UpdateSourceAsync(string sourceId, UpdateSourceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SourceDto> EnableSourceAsync(string sourceId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SourceDto> DisableSourceAsync(string sourceId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SourceDto> AssignCategoriesAsync(string sourceId, AssignSourceCategoriesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SourceDto> UpdateThrottlingAsync(string sourceId, UpdateSourceThrottlingRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CrawlJobListResponseDto> GetCrawlJobsAsync(CrawlJobQueryDto? query = null, CancellationToken cancellationToken = default) => Task.FromResult(CrawlJobsPage);
        public Task<CrawlJobDto?> GetCrawlJobAsync(string jobId, CancellationToken cancellationToken = default) => Task.FromResult(CrawlJob);
        public Task<CrawlJobDto> CreateCrawlJobAsync(CreateCrawlJobRequest request, CancellationToken cancellationToken = default)
        {
            LastCreatedJobRequest = request;
            return Task.FromResult(CreatedJob ?? new CrawlJobDto { JobId = "job_default" });
        }
        public Task<CrawlJobDto> CancelCrawlJobAsync(string jobId, CancellationToken cancellationToken = default)
        {
            LastCancelledJobId = jobId;
            return Task.FromResult(CancelledJob ?? new CrawlJobDto { JobId = jobId, Status = "cancel_requested" });
        }
        public Task<ProductListResponseDto> GetProductsAsync(ProductListQueryDto? query = null, CancellationToken cancellationToken = default) => Task.FromResult(ProductPage);
        public Task<ProductDetailDto?> GetProductAsync(string productId, CancellationToken cancellationToken = default) => Task.FromResult(Product);
        public Task<IReadOnlyList<ProductChangeEventDto>> GetProductHistoryAsync(string productId, CancellationToken cancellationToken = default) => Task.FromResult(ProductHistory);
    }
}
