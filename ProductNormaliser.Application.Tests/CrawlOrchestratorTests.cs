using Microsoft.Extensions.Logging;
using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Crawling;
using ProductNormaliser.Infrastructure.Discovery;
using ProductNormaliser.Infrastructure.Mongo.Repositories;
using ProductNormaliser.Infrastructure.StructuredData;
using ProductNormaliser.Worker;

namespace ProductNormaliser.Tests;

public sealed class CrawlOrchestratorTests
{
    [Test]
    public async Task ProcessAsync_CallsPipelineStepsInExpectedOrder()
    {
        var calls = new List<string>();
        var target = CreateTarget();
        var orchestrator = CreateOrchestrator(
            calls,
            new FakeRobotsPolicyService(calls, allowed: true),
            new FakeHttpFetcher(calls, success: true, html: "<html>tv</html>"),
            new FakeDeltaProcessor(calls, unchanged: false),
            new FakeStructuredDataExtractor(calls, [CreateExtractedProduct(target.Url)]),
            new FakeSourceProductBuilder(calls, CreateSourceProduct("source-1", target.Url)),
            new FakeAttributeNormaliser(calls),
            new FakeSourceProductStore(calls),
            new FakeCanonicalProductStore(calls),
            new FakeProductIdentityResolver(calls),
            new FakeCanonicalMergeService(calls),
            new FakeProductOfferStore(calls),
            new FakeConflictDetector(calls),
            new FakeMergeConflictStore(calls));

        var result = await orchestrator.ProcessAsync(target, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo("completed"));
            Assert.That(calls, Is.EqualTo(new[]
            {
                "robots",
                "fetch",
                "delta",
                "raw-pages.upsert",
                "extract",
                "build-source-product",
                "normalise",
                "source-products.upsert",
                "canonical-products.list-potential",
                "identity-match",
                "merge",
                "canonical-products.upsert",
                "offers.upsert",
                "detect-conflicts",
                "conflicts.upsert",
                "expand-related-links"
            }));
        });
    }

    [Test]
    public async Task ProcessAsync_CompletesWhenRelatedLinkExpansionFails()
    {
        var calls = new List<string>();
        var orchestrator = CreateOrchestrator(
            calls,
            new FakeRobotsPolicyService(calls, allowed: true),
            new FakeHttpFetcher(calls, success: true, html: "<html>tv</html>"),
            new FakeDeltaProcessor(calls, unchanged: false),
            new FakeStructuredDataExtractor(calls, [CreateExtractedProduct("https://example.com/products/1")]),
            new FakeSourceProductBuilder(calls, CreateSourceProduct("source-1", "https://example.com/products/1")),
            new FakeAttributeNormaliser(calls),
            new FakeSourceProductStore(calls),
            new FakeCanonicalProductStore(calls),
            new FakeProductIdentityResolver(calls),
            new FakeCanonicalMergeService(calls),
            new FakeProductOfferStore(calls),
            new FakeConflictDetector(calls),
            new FakeMergeConflictStore(calls),
            new ThrowingRelatedLinkExpansionService());

        var result = await orchestrator.ProcessAsync(CreateTarget(), CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo("completed"));
    }

    [Test]
    public async Task ProcessAsync_SkipsBlockedRobotsPages()
    {
        var calls = new List<string>();
        var orchestrator = CreateOrchestrator(
            calls,
            new FakeRobotsPolicyService(calls, allowed: false),
            new FakeHttpFetcher(calls, success: true, html: "<html />"),
            new FakeDeltaProcessor(calls, unchanged: false),
            new FakeStructuredDataExtractor(calls, []),
            new FakeSourceProductBuilder(calls, CreateSourceProduct("source-1", "https://example.com/products/1")),
            new FakeAttributeNormaliser(calls),
            new FakeSourceProductStore(calls),
            new FakeCanonicalProductStore(calls),
            new FakeProductIdentityResolver(calls),
            new FakeCanonicalMergeService(calls),
            new FakeProductOfferStore(calls),
            new FakeConflictDetector(calls),
            new FakeMergeConflictStore(calls));

        var result = await orchestrator.ProcessAsync(CreateTarget(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo("skipped"));
            Assert.That(calls, Is.EqualTo(new[] { "robots" }));
        });
    }

    [Test]
    public async Task ProcessAsync_StopsAfterPersistingRawPageWhenDeltaIsUnchanged()
    {
        var calls = new List<string>();
        var orchestrator = CreateOrchestrator(
            calls,
            new FakeRobotsPolicyService(calls, allowed: true),
            new FakeHttpFetcher(calls, success: true, html: "<html>same</html>"),
            new FakeDeltaProcessor(calls, unchanged: true),
            new FakeStructuredDataExtractor(calls, [CreateExtractedProduct("https://example.com/products/1")]),
            new FakeSourceProductBuilder(calls, CreateSourceProduct("source-1", "https://example.com/products/1")),
            new FakeAttributeNormaliser(calls),
            new FakeSourceProductStore(calls),
            new FakeCanonicalProductStore(calls),
            new FakeProductIdentityResolver(calls),
            new FakeCanonicalMergeService(calls),
            new FakeProductOfferStore(calls),
            new FakeConflictDetector(calls),
            new FakeMergeConflictStore(calls));

        var result = await orchestrator.ProcessAsync(CreateTarget(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo("skipped"));
            Assert.That(calls, Is.EqualTo(new[]
            {
                "robots",
                "fetch",
                "delta",
                "raw-pages.upsert"
            }));
        });
    }

    private static CrawlTarget CreateTarget()
    {
        return new CrawlTarget
        {
            Url = "https://example.com/products/1",
            CategoryKey = "tv",
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceName"] = "example-retailer"
            }
        };
    }

    private static ExtractedStructuredProduct CreateExtractedProduct(string url)
    {
        return new ExtractedStructuredProduct
        {
            SourceUrl = url,
            Name = "Test TV",
            Brand = "Samsung",
            ModelNumber = "QE55S90D",
            Attributes = new Dictionary<string, string>
            {
                ["Screen Size"] = "55 in"
            },
            RawJson = "{}",
            Offers =
            [
                new ExtractedOffer
                {
                    Price = 1000m,
                    Currency = "GBP",
                    Availability = "InStock",
                    RawJson = "{}"
                }
            ]
        };
    }

    private static SourceProduct CreateSourceProduct(string id, string sourceUrl)
    {
        return new SourceProduct
        {
            Id = id,
            SourceName = "example-retailer",
            SourceUrl = sourceUrl,
            CategoryKey = "tv",
            Brand = "Samsung",
            ModelNumber = "QE55S90D",
            Title = "Test TV",
            RawSchemaJson = "{}",
            FetchedUtc = new DateTime(2026, 03, 20, 12, 00, 00, DateTimeKind.Utc),
            Offers =
            [
                new ProductOffer
                {
                    Id = "offer-1",
                    SourceName = "example-retailer",
                    SourceUrl = sourceUrl,
                    Price = 1000m,
                    Currency = "GBP",
                    Availability = "InStock",
                    ObservedUtc = new DateTime(2026, 03, 20, 12, 00, 00, DateTimeKind.Utc)
                }
            ]
        };
    }

    private static CrawlOrchestrator CreateOrchestrator(
        List<string> calls,
        IRobotsPolicyService robotsPolicyService,
        IHttpFetcher httpFetcher,
        IDeltaProcessor deltaProcessor,
        IStructuredDataExtractor structuredDataExtractor,
        ISourceProductBuilder sourceProductBuilder,
        IAttributeNormaliser attributeNormaliser,
        ISourceProductStore sourceProductStore,
        ICanonicalProductStore canonicalProductStore,
        IProductIdentityResolver productIdentityResolver,
        ICanonicalMergeService canonicalMergeService,
        IProductOfferStore productOfferStore,
        IConflictDetector conflictDetector,
        IMergeConflictStore mergeConflictStore,
        IRelatedLinkExpansionService? relatedLinkExpansionService = null)
    {
        return new CrawlOrchestrator(
            robotsPolicyService,
            httpFetcher,
            deltaProcessor,
            new FakeSourceTrustService(),
            new FakeSourceDisagreementService(),
            new FakeRawPageStore(calls),
            structuredDataExtractor,
            sourceProductBuilder,
            attributeNormaliser,
            sourceProductStore,
            canonicalProductStore,
            new FakeProductChangeEventStore(),
            productIdentityResolver,
            canonicalMergeService,
            productOfferStore,
            conflictDetector,
            mergeConflictStore,
                relatedLinkExpansionService ?? new FakeRelatedLinkExpansionService(calls),
            new FakeCrawlLogStore(),
            new TestLogger<CrawlOrchestrator>());
    }

    private sealed class FakeRobotsPolicyService(List<string> calls, bool allowed) : IRobotsPolicyService
    {
        public Task<RobotsPolicyDecision> EvaluateAsync(CrawlTarget target, CancellationToken cancellationToken)
        {
            calls.Add("robots");
            return Task.FromResult(new RobotsPolicyDecision { IsAllowed = allowed, Reason = allowed ? "allowed" : "blocked" });
        }
    }

    private sealed class FakeHttpFetcher(List<string> calls, bool success, string html) : IHttpFetcher
    {
        public Task<FetchResult> FetchAsync(CrawlTarget target, CancellationToken cancellationToken)
        {
            calls.Add("fetch");
            return Task.FromResult(new FetchResult
            {
                Url = target.Url,
                IsSuccess = success,
                StatusCode = success ? 200 : 500,
                Html = html,
                FailureReason = success ? null : "failed",
                FetchedUtc = new DateTime(2026, 03, 20, 12, 00, 00, DateTimeKind.Utc)
            });
        }
    }

    private sealed class FakeDeltaProcessor(List<string> calls, bool unchanged) : IDeltaProcessor
    {
        public Task<DeltaDetectionResult> DetectAsync(string sourceName, string sourceUrl, string html, CancellationToken cancellationToken)
        {
            calls.Add("delta");
            return Task.FromResult(new DeltaDetectionResult { IsUnchanged = unchanged, ContentHash = "ABC123" });
        }

        public Task<SemanticDeltaResult> DetectSemanticChangesAsync(SourceProduct sourceProduct, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SemanticDeltaResult { HasMeaningfulChanges = true, HasAttributeChanges = true, ChangedAttributeKeys = ["screen_size_inch"], Summary = "Spec changes: screen_size_inch" });
        }

        public IReadOnlyList<ProductChangeEvent> BuildChangeEvents(CanonicalProduct? previousCanonical, CanonicalProduct currentCanonical, SourceProduct sourceProduct, SemanticDeltaResult semanticDelta)
            => [];

        public string ComputeHash(string html) => "ABC123";
    }

    private sealed class FakeStructuredDataExtractor(List<string> calls, IReadOnlyCollection<ExtractedStructuredProduct> products) : IStructuredDataExtractor
    {
        public IReadOnlyCollection<ExtractedStructuredProduct> ExtractProducts(string html, string url)
        {
            calls.Add("extract");
            return products;
        }
    }

    private sealed class FakeSourceProductBuilder(List<string> calls, SourceProduct sourceProduct) : ISourceProductBuilder
    {
        public SourceProduct Build(string sourceName, string categoryKey, ExtractedStructuredProduct extractedProduct, DateTime fetchedUtc)
        {
            calls.Add("build-source-product");
            return sourceProduct;
        }
    }

    private sealed class FakeAttributeNormaliser(List<string> calls) : IAttributeNormaliser
    {
        public Dictionary<string, NormalisedAttributeValue> Normalise(string categoryKey, Dictionary<string, SourceAttributeValue> rawAttributes)
        {
            calls.Add("normalise");
            return new Dictionary<string, NormalisedAttributeValue>
            {
                ["screen_size_inch"] = new()
                {
                    AttributeKey = "screen_size_inch",
                    Value = 55m,
                    ValueType = "decimal",
                    Unit = "inch",
                    Confidence = 0.96m,
                    SourceAttributeKey = "Screen Size",
                    OriginalValue = "55 in"
                }
            };
        }
    }

    private sealed class FakeRawPageStore(List<string> calls) : IRawPageStore
    {
        public Task<RawPage?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<RawPage?>(null);

        public Task<RawPage?> GetLatestBySourceAsync(string sourceName, string sourceUrl, CancellationToken cancellationToken = default) => Task.FromResult<RawPage?>(null);

        public Task UpsertAsync(RawPage page, CancellationToken cancellationToken = default)
        {
            calls.Add("raw-pages.upsert");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSourceProductStore(List<string> calls) : ISourceProductStore
    {
        public Task<SourceProduct?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<SourceProduct?>(null);
        public Task<SourceProduct?> GetBySourceAsync(string sourceName, string sourceUrl, CancellationToken cancellationToken = default) => Task.FromResult<SourceProduct?>(null);
        public Task UpsertAsync(SourceProduct product, CancellationToken cancellationToken = default)
        {
            calls.Add("source-products.upsert");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCanonicalProductStore(List<string> calls) : ICanonicalProductStore
    {
        public Task<CanonicalProduct?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<CanonicalProduct?>(null);
        public Task<CanonicalProduct?> GetByGtinAsync(string gtin, CancellationToken cancellationToken = default) => Task.FromResult<CanonicalProduct?>(null);
        public Task<CanonicalProduct?> GetByBrandAndModelAsync(string brand, string modelNumber, CancellationToken cancellationToken = default) => Task.FromResult<CanonicalProduct?>(null);

        public Task<IReadOnlyList<CanonicalProduct>> ListPotentialMatchesAsync(string categoryKey, string? brand, CancellationToken cancellationToken = default)
        {
            calls.Add("canonical-products.list-potential");
            return Task.FromResult<IReadOnlyList<CanonicalProduct>>([]);
        }

        public Task UpsertAsync(CanonicalProduct product, CancellationToken cancellationToken = default)
        {
            calls.Add("canonical-products.upsert");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProductIdentityResolver(List<string> calls) : IProductIdentityResolver
    {
        public ProductIdentityMatchResult Match(SourceProduct sourceProduct, IReadOnlyCollection<CanonicalProduct> candidates)
        {
            calls.Add("identity-match");
            return new ProductIdentityMatchResult { IsMatch = false, Confidence = 0m };
        }
    }

    private sealed class FakeCanonicalMergeService(List<string> calls) : ICanonicalMergeService
    {
        public CanonicalProduct Merge(CanonicalProduct? existing, SourceProduct incoming)
        {
            calls.Add("merge");
            return new CanonicalProduct
            {
                Id = "canonical-1",
                CategoryKey = incoming.CategoryKey,
                Brand = incoming.Brand ?? string.Empty,
                ModelNumber = incoming.ModelNumber,
                DisplayName = incoming.Title ?? incoming.Id,
                CreatedUtc = incoming.FetchedUtc,
                UpdatedUtc = incoming.FetchedUtc,
                Attributes = new Dictionary<string, CanonicalAttributeValue>
                {
                    ["screen_size_inch"] = new()
                    {
                        AttributeKey = "screen_size_inch",
                        Value = 55m,
                        ValueType = "decimal",
                        Unit = "inch",
                        Confidence = 0.96m,
                        Evidence =
                        [
                            new AttributeEvidence
                            {
                                SourceName = incoming.SourceName,
                                SourceUrl = incoming.SourceUrl,
                                SourceProductId = incoming.Id,
                                SourceAttributeKey = "Screen Size",
                                RawValue = "55 in",
                                Confidence = 0.96m,
                                ObservedUtc = incoming.FetchedUtc
                            }
                        ]
                    }
                }
            };
        }
    }

    private sealed class FakeProductOfferStore(List<string> calls) : IProductOfferStore
    {
        public Task<ProductOffer?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<ProductOffer?>(null);
        public Task<IReadOnlyList<ProductOffer>> GetByCanonicalProductIdAsync(string canonicalProductId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductOffer>>([]);
        public Task UpsertAsync(ProductOffer offer, CancellationToken cancellationToken = default)
        {
            calls.Add("offers.upsert");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeConflictDetector(List<string> calls) : IConflictDetector
    {
        public List<MergeConflict> Detect(CanonicalProduct product)
        {
            calls.Add("detect-conflicts");
            return [new MergeConflict
            {
                Id = "conflict-1",
                CanonicalProductId = product.Id,
                AttributeKey = "screen_size_inch",
                Reason = "demo",
                Status = "open",
                CreatedUtc = product.UpdatedUtc
            }];
        }
    }

    private sealed class FakeMergeConflictStore(List<string> calls) : IMergeConflictStore
    {
        public Task<MergeConflict?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<MergeConflict?>(null);
        public Task<IReadOnlyList<MergeConflict>> GetByCanonicalProductIdAndStatusAsync(string canonicalProductId, string status, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MergeConflict>>([]);
        public Task UpsertAsync(MergeConflict conflict, CancellationToken cancellationToken = default)
        {
            calls.Add("conflicts.upsert");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCrawlLogStore : ICrawlLogStore
    {
        public Task<CrawlLog?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<CrawlLog?>(null);

        public Task<IReadOnlyList<CrawlLog>> ListAsync(int limit = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CrawlLog>>([]);

        public Task InsertAsync(CrawlLog log, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSourceTrustService : ISourceTrustService
    {
        public void CaptureSnapshot(string sourceName, string categoryKey)
        {
        }

        public decimal GetHistoricalTrustScore(string sourceName, string categoryKey) => 0.75m;

        public IReadOnlyList<SourceQualitySnapshot> GetSourceHistory(string categoryKey, string? sourceName = null, int? timeRangeDays = null, int limit = 30) => [];
    }

    private sealed class FakeProductChangeEventStore : IProductChangeEventStore
    {
        public Task InsertManyAsync(IReadOnlyCollection<ProductChangeEvent> changeEvents, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<ProductChangeEvent>> GetByCanonicalProductIdAsync(string canonicalProductId, int limit = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductChangeEvent>>([]);

        public Task<IReadOnlyList<ProductChangeEvent>> ListByCategoryAsync(string categoryKey, int limit = 500, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductChangeEvent>>([]);
    }

    private sealed class FakeSourceDisagreementService : ISourceDisagreementService
    {
        public decimal GetSourceAttributeAdjustment(string sourceName, string categoryKey, string attributeKey) => 1.00m;

        public IReadOnlyList<SourceAttributeDisagreement> GetDisagreements(string categoryKey, string? sourceName = null, int? timeRangeDays = null) => [];

        public void RefreshForProduct(CanonicalProduct product)
        {
        }
    }

    private sealed class FakeRelatedLinkExpansionService(List<string> calls) : IRelatedLinkExpansionService
    {
        public Task<RelatedLinkExpansionResult> ExpandAsync(CrawlTarget target, string html, CancellationToken cancellationToken)
        {
            calls.Add("expand-related-links");
            return Task.FromResult(new RelatedLinkExpansionResult(1, 2, 1));
        }
    }

    private sealed class ThrowingRelatedLinkExpansionService : IRelatedLinkExpansionService
    {
        public Task<RelatedLinkExpansionResult> ExpandAsync(CrawlTarget target, string html, CancellationToken cancellationToken)
            => throw new InvalidOperationException("related link expansion failed");
    }
}