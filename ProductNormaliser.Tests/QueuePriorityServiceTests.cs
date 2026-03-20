using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Crawling;
using ProductNormaliser.Infrastructure.Mongo;

namespace ProductNormaliser.Tests;

public sealed class QueuePriorityServiceTests
{
    [SetUp]
    public async Task SetUpAsync()
    {
        var context = MongoIntegrationTestFixture.Context;
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.CrawlQueue);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.CrawlLogs);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.SourceProducts);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.CanonicalProducts);
        await context.EnsureIndexesAsync();
    }

    [Test]
    public async Task GetPrioritiesAsync_OrdersBySourceQualityChangeRateAndMissingAttributes()
    {
        var context = MongoIntegrationTestFixture.Context;
        var now = new DateTime(2026, 03, 20, 12, 00, 00, DateTimeKind.Utc);

        await context.CrawlQueueItems.InsertManyAsync(
        [
            new CrawlQueueItem { Id = "queue-1", SourceName = "trusted-source", SourceUrl = "https://trusted/item", CategoryKey = "tv", Status = "queued", EnqueuedUtc = now.AddHours(-2) },
            new CrawlQueueItem { Id = "queue-2", SourceName = "low-value-source", SourceUrl = "https://low/item", CategoryKey = "tv", Status = "queued", EnqueuedUtc = now.AddHours(-1) }
        ]);

        await context.SourceProducts.InsertManyAsync(
        [
            new SourceProduct
            {
                Id = "source-1",
                SourceName = "trusted-source",
                SourceUrl = "https://trusted/item",
                CategoryKey = "tv",
                Brand = "Sony",
                ModelNumber = "A95L",
                Title = "Sony A95L",
                RawSchemaJson = "{}",
                FetchedUtc = now.AddDays(-3),
                NormalisedAttributes = new Dictionary<string, NormalisedAttributeValue>
                {
                    ["screen_size_inch"] = new() { AttributeKey = "screen_size_inch", Value = 55m, ValueType = "decimal", Confidence = 0.98m },
                    ["native_resolution"] = new() { AttributeKey = "native_resolution", Value = "4K", ValueType = "string", Confidence = 0.98m },
                    ["display_technology"] = new() { AttributeKey = "display_technology", Value = "OLED", ValueType = "string", Confidence = 0.97m }
                }
            },
            new SourceProduct
            {
                Id = "source-2",
                SourceName = "low-value-source",
                SourceUrl = "https://low/item",
                CategoryKey = "tv",
                Brand = "Sony",
                ModelNumber = "A80L",
                Title = "Sony A80L",
                RawSchemaJson = "{}",
                FetchedUtc = now.AddDays(-1),
                NormalisedAttributes = new Dictionary<string, NormalisedAttributeValue>
                {
                    ["screen_size_inch"] = new() { AttributeKey = "screen_size_inch", Value = 55m, ValueType = "decimal", Confidence = 0.55m }
                }
            }
        ]);

        await context.CanonicalProducts.InsertManyAsync(
        [
            new CanonicalProduct
            {
                Id = "canonical-1",
                CategoryKey = "tv",
                Brand = "Sony",
                ModelNumber = "A95L",
                DisplayName = "Sony A95L",
                CreatedUtc = now.AddDays(-3),
                UpdatedUtc = now.AddDays(-1),
                Sources = [new ProductSourceLink { SourceName = "trusted-source", SourceProductId = "source-1", SourceUrl = "https://trusted/item", FirstSeenUtc = now.AddDays(-3), LastSeenUtc = now.AddDays(-1) }],
                Attributes = new Dictionary<string, CanonicalAttributeValue>()
            },
            new CanonicalProduct
            {
                Id = "canonical-2",
                CategoryKey = "tv",
                Brand = "Sony",
                ModelNumber = "A80L",
                DisplayName = "Sony A80L",
                CreatedUtc = now.AddDays(-3),
                UpdatedUtc = now.AddDays(-1),
                Sources = [new ProductSourceLink { SourceName = "low-value-source", SourceProductId = "source-2", SourceUrl = "https://low/item", FirstSeenUtc = now.AddDays(-3), LastSeenUtc = now.AddDays(-1) }],
                Attributes = new Dictionary<string, CanonicalAttributeValue>
                {
                    ["screen_size_inch"] = new() { AttributeKey = "screen_size_inch", Value = 55m, ValueType = "decimal", Confidence = 0.60m }
                }
            }
        ]);

        await context.CrawlLogs.InsertManyAsync(
        [
            new CrawlLog { Id = "log-1", SourceName = "trusted-source", Url = "https://trusted/item", Status = "completed", HadMeaningfulChange = true, TimestampUtc = now.AddDays(-1) },
            new CrawlLog { Id = "log-2", SourceName = "trusted-source", Url = "https://trusted/item", Status = "completed", HadMeaningfulChange = true, TimestampUtc = now.AddDays(-2) },
            new CrawlLog { Id = "log-3", SourceName = "low-value-source", Url = "https://low/item", Status = "completed", HadMeaningfulChange = false, TimestampUtc = now.AddHours(-12) }
        ]);

        var service = new CrawlPriorityService(context);

        var priorities = await service.GetPrioritiesAsync(now, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(priorities, Has.Count.EqualTo(2));
            Assert.That(priorities[0].QueueItem.Id, Is.EqualTo("queue-1"));
            Assert.That(priorities[0].PriorityScore, Is.GreaterThan(priorities[1].PriorityScore));
            Assert.That(priorities[0].Reasons, Contains.Item("High-value source quality"));
        });
    }
}