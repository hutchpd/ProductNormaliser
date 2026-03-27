using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Crawling;
using ProductNormaliser.Infrastructure.Mongo.Repositories;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.Intelligence)]
public sealed class DeltaProcessorTests
{
    [Test]
    public async Task DetectSemanticChangesAsync_FindsSpecAndOfferChanges()
    {
        var previous = new SourceProduct
        {
            Id = "source-1",
            SourceName = "example-source",
            SourceUrl = "https://example.com/tv/1",
            CategoryKey = "tv",
            RawSchemaJson = "{}",
            FetchedUtc = new DateTime(2026, 03, 19, 10, 00, 00, DateTimeKind.Utc),
            NormalisedAttributes = new Dictionary<string, NormalisedAttributeValue>
            {
                ["screen_size_inch"] = new() { AttributeKey = "screen_size_inch", Value = 55m, ValueType = "decimal", Confidence = 0.95m }
            },
            Offers = [new ProductOffer { Id = "offer-1", SourceName = "example-source", SourceUrl = "https://example.com/tv/1", Price = 999m, Currency = "GBP", Availability = "InStock", ObservedUtc = DateTime.UtcNow }]
        };

        var current = new SourceProduct
        {
            Id = "source-1",
            SourceName = "example-source",
            SourceUrl = "https://example.com/tv/1",
            CategoryKey = "tv",
            RawSchemaJson = "{}",
            FetchedUtc = new DateTime(2026, 03, 20, 10, 00, 00, DateTimeKind.Utc),
            NormalisedAttributes = new Dictionary<string, NormalisedAttributeValue>
            {
                ["screen_size_inch"] = new() { AttributeKey = "screen_size_inch", Value = 65m, ValueType = "decimal", Confidence = 0.95m },
                ["smart_tv"] = new() { AttributeKey = "smart_tv", Value = true, ValueType = "boolean", Confidence = 0.97m }
            },
            Offers = [new ProductOffer { Id = "offer-1", SourceName = "example-source", SourceUrl = "https://example.com/tv/1", Price = 899m, Currency = "GBP", Availability = "LimitedStock", ObservedUtc = DateTime.UtcNow }]
        };

        var sut = new DeltaProcessor(new FakeRawPageStore(), new FakeSourceProductStore(previous));

        var result = await sut.DetectSemanticChangesAsync(current, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasMeaningfulChanges, Is.True);
            Assert.That(result.HasAttributeChanges, Is.True);
            Assert.That(result.HasOfferChanges, Is.True);
            Assert.That(result.PriceChanged, Is.True);
            Assert.That(result.AvailabilityChanged, Is.True);
            Assert.That(result.ChangedAttributeKeys, Is.EquivalentTo(new[] { "screen_size_inch", "smart_tv" }));
        });
    }

    private sealed class FakeRawPageStore : IRawPageStore
    {
        public Task<RawPage?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<RawPage?>(null);
        public Task<RawPage?> GetLatestBySourceAsync(string sourceName, string sourceUrl, CancellationToken cancellationToken = default) => Task.FromResult<RawPage?>(null);
        public Task UpsertAsync(RawPage page, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeSourceProductStore(SourceProduct? existing) : ISourceProductStore
    {
        public Task<SourceProduct?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<SourceProduct?>(existing);
        public Task<SourceProduct?> GetBySourceAsync(string sourceName, string sourceUrl, CancellationToken cancellationToken = default) => Task.FromResult<SourceProduct?>(existing);
        public Task UpsertAsync(SourceProduct product, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}