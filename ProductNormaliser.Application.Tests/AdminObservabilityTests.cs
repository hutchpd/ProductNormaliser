using ProductNormaliser.AdminApi.Services;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Mongo;
using ProductNormaliser.Infrastructure.Mongo.Repositories;
using ProductNormaliser.Worker;

namespace ProductNormaliser.Tests;

public sealed class AdminObservabilityTests
{
    [SetUp]
    public async Task SetUpAsync()
    {
        var context = MongoIntegrationTestFixture.Context;
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.CrawlLogs);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.CanonicalProducts);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.SourceProducts);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.MergeConflicts);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.CrawlQueue);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.ProductChangeEvents);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.SourceQualitySnapshots);
        await context.EnsureIndexesAsync();
    }

    [Test]
    public async Task CrawlLogs_AreWrittenCorrectly()
    {
        var calls = new List<string>();
        var crawlLogStore = new CrawlLogRepository(MongoIntegrationTestFixture.Context);
        var orchestrator = new CrawlOrchestrator(
            new FakeRobotsPolicyService(true),
            new FakeHttpFetcher(true, "<html />"),
            new FakeDeltaProcessor(false),
            new FakeSourceTrustService(),
            new FakeSourceDisagreementService(),
            new RawPageRepository(MongoIntegrationTestFixture.Context),
            new FakeStructuredDataExtractor([new ExtractedStructuredProduct { SourceUrl = "https://example.com/products/1", Name = "TV", RawJson = "{}" }]),
            new FakeSourceProductBuilder(),
            new FakeAttributeNormaliser(),
            new SourceProductRepository(MongoIntegrationTestFixture.Context),
            new CanonicalProductRepository(MongoIntegrationTestFixture.Context),
            new ProductChangeEventRepository(MongoIntegrationTestFixture.Context),
            new FakeIdentityResolver(),
            new FakeMergeService(),
            new ProductOfferRepository(MongoIntegrationTestFixture.Context),
            new FakeConflictDetector(),
            new MergeConflictRepository(MongoIntegrationTestFixture.Context),
            crawlLogStore,
            new TestLogger<CrawlOrchestrator>());

        var result = await orchestrator.ProcessAsync(new CrawlTarget
        {
            Url = "https://example.com/products/1",
            CategoryKey = "tv",
            Metadata = new Dictionary<string, string> { ["sourceName"] = "example-retailer" }
        }, CancellationToken.None);

        var logs = await crawlLogStore.ListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo("completed"));
            Assert.That(logs, Has.Count.EqualTo(1));
            Assert.That(logs[0].SourceName, Is.EqualTo("example-retailer"));
            Assert.That(logs[0].Status, Is.EqualTo("completed"));
            Assert.That(logs[0].ExtractedProductCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Stats_AreCalculatedCorrectly()
    {
        var context = MongoIntegrationTestFixture.Context;
        await context.CanonicalProducts.InsertManyAsync(
        [
            new CanonicalProduct
            {
                Id = "canonical-1",
                CategoryKey = "tv",
                Brand = "Samsung",
                ModelNumber = "QE55S90D",
                DisplayName = "Samsung TV",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                Attributes = new Dictionary<string, CanonicalAttributeValue>
                {
                    ["screen_size_inch"] = new() { AttributeKey = "screen_size_inch", Value = 55m, ValueType = "decimal", Confidence = 0.9m },
                    ["native_resolution"] = new() { AttributeKey = "native_resolution", Value = "4K", ValueType = "string", Confidence = 0.9m },
                    ["display_technology"] = new() { AttributeKey = "display_technology", Value = "OLED", ValueType = "string", Confidence = 0.9m }
                }
            },
            new CanonicalProduct
            {
                Id = "canonical-2",
                CategoryKey = "tv",
                Brand = "LG",
                ModelNumber = "OLED55C4",
                DisplayName = "LG TV",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                Attributes = new Dictionary<string, CanonicalAttributeValue>
                {
                    ["screen_size_inch"] = new() { AttributeKey = "screen_size_inch", Value = 55m, ValueType = "decimal", Confidence = 0.9m, HasConflict = true }
                }
            }
        ]);

        await context.SourceProducts.InsertManyAsync(
        [
            new SourceProduct { Id = "source-1", SourceName = "a", SourceUrl = "https://a/1", CategoryKey = "tv", RawSchemaJson = "{}", FetchedUtc = DateTime.UtcNow },
            new SourceProduct { Id = "source-2", SourceName = "b", SourceUrl = "https://b/1", CategoryKey = "tv", RawSchemaJson = "{}", FetchedUtc = DateTime.UtcNow },
            new SourceProduct { Id = "source-3", SourceName = "c", SourceUrl = "https://c/1", CategoryKey = "tv", RawSchemaJson = "{}", FetchedUtc = DateTime.UtcNow }
        ]);

        await context.CrawlSources.InsertManyAsync(
        [
            new CrawlSource { Id = "ao", DisplayName = "AO UK", BaseUrl = "https://ao.example", Host = "ao.example", IsEnabled = true, SupportedCategoryKeys = ["tv"], CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow },
            new CrawlSource { Id = "northwind", DisplayName = "Northwind", BaseUrl = "https://northwind.example", Host = "northwind.example", IsEnabled = true, SupportedCategoryKeys = ["monitor"], CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow }
        ]);

        await context.CrawlJobs.InsertManyAsync(
        [
            new CrawlJob
            {
                JobId = "job_active",
                RequestType = CrawlJobRequestTypes.Category,
                RequestedCategories = ["tv"],
                TotalTargets = 2,
                ProcessedTargets = 1,
                SuccessCount = 1,
                Status = CrawlJobStatuses.Running,
                StartedAt = DateTime.UtcNow.AddMinutes(-30),
                LastUpdatedAt = DateTime.UtcNow.AddMinutes(-5),
                PerCategoryBreakdown = [new CrawlJobCategoryBreakdown { CategoryKey = "tv", TotalTargets = 2, ProcessedTargets = 1, SuccessCount = 1 }]
            }
        ]);

        await context.CrawlQueueItems.InsertManyAsync(
        [
            new CrawlQueueItem
            {
                Id = "queue_tv_retry",
                JobId = "job_active",
                SourceName = "AO UK",
                SourceUrl = "https://ao.example/tv-1",
                CategoryKey = "tv",
                Status = "queued",
                AttemptCount = 2,
                ConsecutiveFailureCount = 2,
                EnqueuedUtc = DateTime.UtcNow.AddMinutes(-25),
                NextAttemptUtc = DateTime.UtcNow.AddMinutes(-1),
                LastError = "timeout"
            },
            new CrawlQueueItem
            {
                Id = "queue_monitor_failed",
                SourceName = "Northwind",
                SourceUrl = "https://northwind.example/monitor-1",
                CategoryKey = "monitor",
                Status = "failed",
                AttemptCount = 3,
                ConsecutiveFailureCount = 3,
                EnqueuedUtc = DateTime.UtcNow.AddMinutes(-20),
                LastAttemptUtc = DateTime.UtcNow.AddMinutes(-10),
                LastError = "blocked"
            }
        ]);

        await context.CrawlLogs.InsertManyAsync(
        [
            new CrawlLog
            {
                Id = "log_1",
                SourceName = "AO UK",
                Url = "https://ao.example/tv-1",
                Status = "completed",
                DurationMs = 850,
                ExtractedProductCount = 4,
                TimestampUtc = DateTime.UtcNow.AddHours(-2)
            },
            new CrawlLog
            {
                Id = "log_2",
                SourceName = "Northwind",
                Url = "https://northwind.example/monitor-1",
                Status = "failed",
                DurationMs = 1200,
                ErrorMessage = "timeout",
                TimestampUtc = DateTime.UtcNow.AddHours(-1)
            }
        ]);

        await context.SourceQualitySnapshots.InsertManyAsync(
        [
            new SourceQualitySnapshot
            {
                Id = "snapshot_1",
                SourceName = "AO UK",
                CategoryKey = "tv",
                TimestampUtc = DateTime.UtcNow.AddMinutes(-40),
                AttributeCoverage = 84m,
                SuccessfulCrawlRate = 95m,
                HistoricalTrustScore = 90m
            },
            new SourceQualitySnapshot
            {
                Id = "snapshot_2",
                SourceName = "Northwind",
                CategoryKey = "monitor",
                TimestampUtc = DateTime.UtcNow.AddMinutes(-40),
                AttributeCoverage = 61m,
                SuccessfulCrawlRate = 68m,
                HistoricalTrustScore = 59m
            }
        ]);

        var service = new AdminQueryService(
            new CrawlLogRepository(context),
            new CanonicalProductRepository(context),
            new SourceProductRepository(context),
            new ProductChangeEventRepository(context),
            context);

        var stats = await service.GetStatsAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(stats.TotalCanonicalProducts, Is.EqualTo(2));
            Assert.That(stats.TotalSourceProducts, Is.EqualTo(3));
            Assert.That(stats.AverageAttributesPerProduct, Is.EqualTo(2.00m));
            Assert.That(stats.PercentProductsWithConflicts, Is.EqualTo(50.00m));
            Assert.That(stats.PercentProductsMissingKeyAttributes, Is.EqualTo(50.00m));
            Assert.That(stats.Operational.ActiveJobCount, Is.EqualTo(1));
            Assert.That(stats.Operational.QueueDepth, Is.EqualTo(1));
            Assert.That(stats.Operational.RetryQueueDepth, Is.EqualTo(1));
            Assert.That(stats.Operational.FailedQueueDepth, Is.EqualTo(1));
            Assert.That(stats.Operational.ThroughputLast24Hours, Is.EqualTo(2));
            Assert.That(stats.Operational.FailureCountLast24Hours, Is.EqualTo(1));
            Assert.That(stats.Operational.HealthySourceCount, Is.EqualTo(1));
            Assert.That(stats.Operational.AttentionSourceCount, Is.EqualTo(1));
            Assert.That(stats.Operational.Sources.Single(metric => metric.SourceName == "AO UK").RetryQueueDepth, Is.EqualTo(1));
            Assert.That(stats.Operational.Sources.Single(metric => metric.SourceName == "Northwind").HealthStatus, Is.EqualTo("Attention"));
            Assert.That(stats.Operational.Categories.Single(metric => metric.CategoryKey == "tv").QueueDepth, Is.EqualTo(1));
            Assert.That(stats.Operational.Categories.Single(metric => metric.CategoryKey == "monitor").FailedCrawlsLast24Hours, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ProductDetail_IncludesEvidenceAndRawSourceValues()
    {
        var context = MongoIntegrationTestFixture.Context;
        await context.SourceProducts.InsertOneAsync(new SourceProduct
        {
            Id = "source-1",
            SourceName = "example-retailer",
            SourceUrl = "https://example.com/tv/1",
            CategoryKey = "tv",
            Brand = "Samsung",
            ModelNumber = "QE55S90D",
            Title = "Samsung TV",
            RawSchemaJson = "{\"name\":\"Samsung TV\"}",
            FetchedUtc = DateTime.UtcNow,
            RawAttributes = new Dictionary<string, SourceAttributeValue>
            {
                ["Screen Size"] = new() { AttributeKey = "Screen Size", Value = "55 in", ValueType = "string", SourcePath = "jsonld.additionalProperty" }
            }
        });

        await context.CanonicalProducts.InsertOneAsync(new CanonicalProduct
        {
            Id = "canonical-1",
            CategoryKey = "tv",
            Brand = "Samsung",
            ModelNumber = "QE55S90D",
            DisplayName = "Samsung TV",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
            Sources =
            [
                new ProductSourceLink
                {
                    SourceName = "example-retailer",
                    SourceProductId = "source-1",
                    SourceUrl = "https://example.com/tv/1",
                    FirstSeenUtc = DateTime.UtcNow,
                    LastSeenUtc = DateTime.UtcNow
                }
            ],
            Attributes = new Dictionary<string, CanonicalAttributeValue>
            {
                ["screen_size_inch"] = new()
                {
                    AttributeKey = "screen_size_inch",
                    Value = 55m,
                    ValueType = "decimal",
                    Unit = "inch",
                    Confidence = 0.97m,
                    Evidence =
                    [
                        new AttributeEvidence
                        {
                            SourceName = "example-retailer",
                            SourceUrl = "https://example.com/tv/1",
                            SourceProductId = "source-1",
                            SourceAttributeKey = "Screen Size",
                            RawValue = "55 in",
                            SelectorOrPath = "Parsed numeric value in inch.",
                            Confidence = 0.97m,
                            ObservedUtc = DateTime.UtcNow
                        }
                    ]
                }
            }
        });

        var service = new AdminQueryService(
            new CrawlLogRepository(context),
            new CanonicalProductRepository(context),
            new SourceProductRepository(context),
            new ProductChangeEventRepository(context),
            context);

        var detail = await service.GetProductAsync("canonical-1", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(detail, Is.Not.Null);
            Assert.That(detail!.Attributes.Single().Evidence.Single().RawValue, Is.EqualTo("55 in"));
            Assert.That(detail.SourceProducts.Single().RawAttributes.Single().Value, Is.EqualTo("55 in"));
            Assert.That(detail.SourceProducts.Single().RawSchemaJson, Does.Contain("Samsung TV"));
        });
    }

    private sealed class FakeRobotsPolicyService(bool allowed) : ProductNormaliser.Infrastructure.Crawling.IRobotsPolicyService
    {
        public Task<ProductNormaliser.Infrastructure.Crawling.RobotsPolicyDecision> EvaluateAsync(CrawlTarget target, CancellationToken cancellationToken)
            => Task.FromResult(new ProductNormaliser.Infrastructure.Crawling.RobotsPolicyDecision { IsAllowed = allowed, Reason = allowed ? "allowed" : "blocked" });
    }

    private sealed class FakeHttpFetcher(bool success, string html) : ProductNormaliser.Infrastructure.Crawling.IHttpFetcher
    {
        public Task<FetchResult> FetchAsync(CrawlTarget target, CancellationToken cancellationToken)
            => Task.FromResult(new FetchResult { Url = target.Url, IsSuccess = success, StatusCode = success ? 200 : 500, Html = html, FetchedUtc = DateTime.UtcNow });
    }

    private sealed class FakeDeltaProcessor(bool unchanged) : ProductNormaliser.Infrastructure.Crawling.IDeltaProcessor
    {
        public Task<ProductNormaliser.Infrastructure.Crawling.DeltaDetectionResult> DetectAsync(string sourceName, string sourceUrl, string html, CancellationToken cancellationToken)
            => Task.FromResult(new ProductNormaliser.Infrastructure.Crawling.DeltaDetectionResult { IsUnchanged = unchanged, ContentHash = "ABC123456789" });

        public Task<ProductNormaliser.Infrastructure.Crawling.SemanticDeltaResult> DetectSemanticChangesAsync(SourceProduct sourceProduct, CancellationToken cancellationToken)
            => Task.FromResult(new ProductNormaliser.Infrastructure.Crawling.SemanticDeltaResult { HasMeaningfulChanges = true, HasAttributeChanges = true, ChangedAttributeKeys = ["screen_size_inch"], Summary = "Spec changes: screen_size_inch" });

        public IReadOnlyList<ProductChangeEvent> BuildChangeEvents(CanonicalProduct? previousCanonical, CanonicalProduct currentCanonical, SourceProduct sourceProduct, ProductNormaliser.Infrastructure.Crawling.SemanticDeltaResult semanticDelta)
            => [];

        public string ComputeHash(string html) => "ABC123456789";
    }

    private sealed class FakeSourceTrustService : ProductNormaliser.Core.Interfaces.ISourceTrustService
    {
        public void CaptureSnapshot(string sourceName, string categoryKey)
        {
        }

        public decimal GetHistoricalTrustScore(string sourceName, string categoryKey) => 0.75m;

        public IReadOnlyList<SourceQualitySnapshot> GetSourceHistory(string categoryKey, string? sourceName = null, int? timeRangeDays = null, int limit = 30) => [];
    }

    private sealed class FakeSourceDisagreementService : ProductNormaliser.Core.Interfaces.ISourceDisagreementService
    {
        public decimal GetSourceAttributeAdjustment(string sourceName, string categoryKey, string attributeKey) => 1.00m;

        public IReadOnlyList<SourceAttributeDisagreement> GetDisagreements(string categoryKey, string? sourceName = null, int? timeRangeDays = null) => [];

        public void RefreshForProduct(CanonicalProduct product)
        {
        }
    }

    private sealed class FakeStructuredDataExtractor(IReadOnlyCollection<ExtractedStructuredProduct> products) : ProductNormaliser.Core.Interfaces.IStructuredDataExtractor
    {
        public IReadOnlyCollection<ExtractedStructuredProduct> ExtractProducts(string html, string url) => products;
    }

    private sealed class FakeSourceProductBuilder : ProductNormaliser.Infrastructure.StructuredData.ISourceProductBuilder
    {
        public SourceProduct Build(string sourceName, string categoryKey, ExtractedStructuredProduct extractedProduct, DateTime fetchedUtc)
        {
            return new SourceProduct
            {
                Id = "source-1",
                SourceName = sourceName,
                SourceUrl = extractedProduct.SourceUrl,
                CategoryKey = categoryKey,
                Brand = "Samsung",
                ModelNumber = "QE55S90D",
                Title = extractedProduct.Name,
                RawSchemaJson = extractedProduct.RawJson,
                FetchedUtc = fetchedUtc,
                Offers = [new ProductOffer { Id = "offer-1", SourceName = sourceName, SourceUrl = extractedProduct.SourceUrl, Price = 100m, Currency = "GBP", Availability = "InStock", ObservedUtc = fetchedUtc }]
            };
        }
    }

    private sealed class FakeAttributeNormaliser : ProductNormaliser.Core.Interfaces.IAttributeNormaliser
    {
        public Dictionary<string, NormalisedAttributeValue> Normalise(string categoryKey, Dictionary<string, SourceAttributeValue> rawAttributes)
            => new()
            {
                ["screen_size_inch"] = new NormalisedAttributeValue { AttributeKey = "screen_size_inch", Value = 55m, ValueType = "decimal", Unit = "inch", Confidence = 0.97m, OriginalValue = "55 in", SourceAttributeKey = "Screen Size" }
            };
    }

    private sealed class FakeIdentityResolver : ProductNormaliser.Core.Interfaces.IProductIdentityResolver
    {
        public ProductIdentityMatchResult Match(SourceProduct sourceProduct, IReadOnlyCollection<CanonicalProduct> candidates)
            => new() { IsMatch = false, Confidence = 0m };
    }

    private sealed class FakeMergeService : ProductNormaliser.Core.Interfaces.ICanonicalMergeService
    {
        public CanonicalProduct Merge(CanonicalProduct? existing, SourceProduct incoming)
        {
            return new CanonicalProduct
            {
                Id = "canonical-1",
                CategoryKey = incoming.CategoryKey,
                Brand = incoming.Brand ?? string.Empty,
                ModelNumber = incoming.ModelNumber,
                DisplayName = incoming.Title ?? incoming.Id,
                CreatedUtc = incoming.FetchedUtc,
                UpdatedUtc = incoming.FetchedUtc
            };
        }
    }

    private sealed class FakeConflictDetector : ProductNormaliser.Core.Interfaces.IConflictDetector
    {
        public List<MergeConflict> Detect(CanonicalProduct product) => [];
    }
}