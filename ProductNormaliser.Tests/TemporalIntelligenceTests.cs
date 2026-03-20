using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Crawling;
using ProductNormaliser.Infrastructure.Intelligence;
using ProductNormaliser.Infrastructure.Mongo;
using ProductNormaliser.Infrastructure.Mongo.Repositories;

namespace ProductNormaliser.Tests;

public sealed class TemporalIntelligenceTests
{
    [SetUp]
    public async Task SetUpAsync()
    {
        var context = MongoIntegrationTestFixture.Context;
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.SourceQualitySnapshots);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.ProductChangeEvents);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.SourceProducts);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.CanonicalProducts);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.CrawlLogs);
        await context.EnsureIndexesAsync();
    }

    [Test]
    public void GetHistoricalTrustScore_AggregatesSnapshotsOverTime()
    {
        var context = MongoIntegrationTestFixture.Context;
        context.SourceQualitySnapshots.InsertMany(
        [
            new SourceQualitySnapshot
            {
                Id = "snapshot-1",
                SourceName = "alpha",
                CategoryKey = "tv",
                TimestampUtc = new DateTime(2026, 03, 01, 10, 00, 00, DateTimeKind.Utc),
                HistoricalTrustScore = 0.40m
            },
            new SourceQualitySnapshot
            {
                Id = "snapshot-2",
                SourceName = "alpha",
                CategoryKey = "tv",
                TimestampUtc = new DateTime(2026, 03, 20, 10, 00, 00, DateTimeKind.Utc),
                HistoricalTrustScore = 0.90m
            }
        ]);

        var service = new SourceTrustService(context);

        var trust = service.GetHistoricalTrustScore("alpha", "tv");

        Assert.That(trust, Is.EqualTo(0.73m).Within(0.01m));
    }

    [Test]
    public void GetScores_FlagsOscillationAndImpossibleValues()
    {
        var context = MongoIntegrationTestFixture.Context;
        context.ProductChangeEvents.InsertMany(
        [
            new ProductChangeEvent { Id = "event-1", CanonicalProductId = "canonical-1", CategoryKey = "tv", AttributeKey = "refresh_rate_hz", OldValue = 120, NewValue = 900, SourceName = "alpha", TimestampUtc = new DateTime(2026, 03, 18, 10, 00, 00, DateTimeKind.Utc) },
            new ProductChangeEvent { Id = "event-2", CanonicalProductId = "canonical-1", CategoryKey = "tv", AttributeKey = "refresh_rate_hz", OldValue = 900, NewValue = 120, SourceName = "alpha", TimestampUtc = new DateTime(2026, 03, 19, 10, 00, 00, DateTimeKind.Utc) },
            new ProductChangeEvent { Id = "event-3", CanonicalProductId = "canonical-1", CategoryKey = "tv", AttributeKey = "refresh_rate_hz", OldValue = 120, NewValue = 900, SourceName = "beta", TimestampUtc = new DateTime(2026, 03, 20, 10, 00, 00, DateTimeKind.Utc) }
        ]);

        var service = new AttributeStabilityService(context);

        var score = service.GetScores("tv").Single(item => item.AttributeKey == "refresh_rate_hz");

        Assert.Multiple(() =>
        {
            Assert.That(score.StabilityScore, Is.LessThan(0.50m));
            Assert.That(score.IsSuspicious, Is.True);
            Assert.That(score.SuspicionReason, Does.Contain("Impossible"));
        });
    }

    [Test]
    public void BuildChangeEvents_CreatesTimelineEntriesForAttributeAndOfferChanges()
    {
        var deltaProcessor = new DeltaProcessor(new FakeRawPageStore(), new FakeSourceProductStore(null));
        var previousCanonical = new CanonicalProduct
        {
            Id = "canonical-1",
            CategoryKey = "tv",
            Brand = "Sony",
            ModelNumber = "A95L",
            DisplayName = "Sony A95L",
            Attributes = new Dictionary<string, CanonicalAttributeValue>
            {
                ["screen_size_inch"] = new() { AttributeKey = "screen_size_inch", Value = 55m, ValueType = "decimal" }
            }
        };
        var currentCanonical = new CanonicalProduct
        {
            Id = "canonical-1",
            CategoryKey = "tv",
            Brand = "Sony",
            ModelNumber = "A95L",
            DisplayName = "Sony A95L",
            Attributes = new Dictionary<string, CanonicalAttributeValue>
            {
                ["screen_size_inch"] = new() { AttributeKey = "screen_size_inch", Value = 65m, ValueType = "decimal" }
            }
        };
        var sourceProduct = new SourceProduct
        {
            Id = "source-1",
            SourceName = "alpha",
            SourceUrl = "https://alpha.example/tv/1",
            CategoryKey = "tv",
            FetchedUtc = new DateTime(2026, 03, 20, 12, 00, 00, DateTimeKind.Utc)
        };
        var semanticDelta = new SemanticDeltaResult
        {
            HasMeaningfulChanges = true,
            ChangeDetails =
            [
                new SemanticChangeDetail { AttributeKey = "offer.price", OldValue = 999m, NewValue = 899m, ChangeType = "offer" },
                new SemanticChangeDetail { AttributeKey = "offer.availability", OldValue = "InStock", NewValue = "LimitedStock", ChangeType = "offer" }
            ]
        };

        var changeEvents = deltaProcessor.BuildChangeEvents(previousCanonical, currentCanonical, sourceProduct, semanticDelta);

        Assert.Multiple(() =>
        {
            Assert.That(changeEvents.Select(changeEvent => changeEvent.AttributeKey), Is.SupersetOf(new[] { "screen_size_inch", "offer.price", "offer.availability" }));
            Assert.That(changeEvents.Single(changeEvent => changeEvent.AttributeKey == "screen_size_inch").NewValue, Is.EqualTo(65m));
            Assert.That(changeEvents.Single(changeEvent => changeEvent.AttributeKey == "offer.price").OldValue, Is.EqualTo(999m));
        });
    }

    private sealed class FakeRawPageStore : IRawPageStore
    {
        public Task<RawPage?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<RawPage?>(null);
        public Task<RawPage?> GetLatestBySourceAsync(string sourceName, string sourceUrl, CancellationToken cancellationToken = default) => Task.FromResult<RawPage?>(null);
        public Task UpsertAsync(RawPage page, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeSourceProductStore(SourceProduct? product) : ISourceProductStore
    {
        public Task<SourceProduct?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(product);
        public Task<SourceProduct?> GetBySourceAsync(string sourceName, string sourceUrl, CancellationToken cancellationToken = default) => Task.FromResult(product);
        public Task UpsertAsync(SourceProduct product, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}