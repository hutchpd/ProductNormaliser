using ProductNormaliser.AdminApi.Services;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;
using ProductNormaliser.Infrastructure.Crawling;
using ProductNormaliser.Infrastructure.Mongo;
using ProductNormaliser.Infrastructure.Mongo.Repositories;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.CategorySchema)]
public sealed class CategorySchemaRegistryConsumptionTests
{
    [SetUp]
    public async Task SetUpAsync()
    {
        var context = MongoIntegrationTestFixture.Context;
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.CanonicalProducts);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.SourceProducts);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.CrawlQueue);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.CrawlLogs);
        await context.EnsureIndexesAsync();
    }

    [Test]
    public async Task DataIntelligenceService_UsesRegistryProvider_ForMonitorSchema()
    {
        var context = MongoIntegrationTestFixture.Context;
        await context.CanonicalProducts.InsertOneAsync(new CanonicalProduct
        {
            Id = "monitor-1",
            CategoryKey = "monitor",
            Brand = "Dell",
            ModelNumber = "U2724D",
            DisplayName = "Dell U2724D",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
            Attributes = new Dictionary<string, CanonicalAttributeValue>
            {
                ["panel_type"] = new() { AttributeKey = "panel_type", Value = "IPS", ValueType = "string", Confidence = 0.95m }
            }
        });

        var registry = new CategorySchemaRegistry([new MonitorCategorySchemaProvider()]);
        var service = new DataIntelligenceService(context, new UnmappedAttributeRepository(context), categorySchemaRegistry: registry);

        var result = await service.GetDetailedCoverageAsync("monitor", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Attributes.Select(attribute => attribute.AttributeKey), Contains.Item("panel_type"));
            Assert.That(result.Attributes.Select(attribute => attribute.AttributeKey), Does.Not.Contain("smart_tv"));
        });
    }

    [Test]
    public async Task CrawlPriorityService_DoesNotFallBackToTvSchema_ForUnknownCategory()
    {
        var context = MongoIntegrationTestFixture.Context;
        var now = new DateTime(2026, 03, 20, 12, 00, 00, DateTimeKind.Utc);

        await context.CrawlQueueItems.InsertOneAsync(new CrawlQueueItem
        {
            Id = "queue-unknown",
            SourceName = "alpha",
            SourceUrl = "https://alpha.example/item/1",
            CategoryKey = "smartwatch",
            Status = "queued",
            EnqueuedUtc = now
        });

        await context.SourceProducts.InsertOneAsync(new SourceProduct
        {
            Id = "source-unknown",
            SourceName = "alpha",
            SourceUrl = "https://alpha.example/item/1",
            CategoryKey = "smartwatch",
            Brand = "Generic",
            ModelNumber = "SW-1",
            RawSchemaJson = "{}",
            FetchedUtc = now
        });

        await context.CanonicalProducts.InsertOneAsync(new CanonicalProduct
        {
            Id = "canonical-unknown",
            CategoryKey = "smartwatch",
            Brand = "Generic",
            ModelNumber = "SW-1",
            DisplayName = "Generic SW-1",
            CreatedUtc = now,
            UpdatedUtc = now,
            Sources = [new ProductSourceLink { SourceName = "alpha", SourceProductId = "source-unknown", SourceUrl = "https://alpha.example/item/1", FirstSeenUtc = now, LastSeenUtc = now }],
            Attributes = new Dictionary<string, CanonicalAttributeValue>()
        });

        var service = new CrawlPriorityService(context, new CategorySchemaRegistry([new TvCategorySchemaProvider()]));

        var result = await service.GetPrioritiesAsync(now, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].MissingAttributeCount, Is.EqualTo(0));
            Assert.That(result[0].Reasons.Any(reason => reason.Contains("15 attributes", StringComparison.OrdinalIgnoreCase)), Is.False);
        });
    }
}