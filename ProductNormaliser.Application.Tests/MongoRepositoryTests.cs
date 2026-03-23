using MongoDB.Driver;
using ProductNormaliser.Application.Crawls;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Mongo;
using ProductNormaliser.Infrastructure.Mongo.Repositories;
using MongoDB.Bson;

namespace ProductNormaliser.Tests;

public sealed class MongoRepositoryTests
{
    private CrawlJobRepository crawlJobRepository = default!;
    private CrawlSourceRepository crawlSourceRepository = default!;
    private RawPageRepository rawPageRepository = default!;
    private SourceProductRepository sourceProductRepository = default!;
    private CanonicalProductRepository canonicalProductRepository = default!;
    private ProductOfferRepository productOfferRepository = default!;
    private MergeConflictRepository mergeConflictRepository = default!;
    private CrawlQueueRepository crawlQueueRepository = default!;
    private DiscoveryQueueRepository discoveryQueueRepository = default!;
    private DiscoveredUrlRepository discoveredUrlRepository = default!;

    [SetUp]
    public async Task SetUpAsync()
    {
        var context = MongoIntegrationTestFixture.Context;
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.CrawlJobs);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.CrawlSources);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.RawPages);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.SourceProducts);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.CanonicalProducts);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.ProductOffers);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.MergeConflicts);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.CrawlQueue);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.DiscoveryQueue);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.DiscoveredUrls);
        await context.EnsureIndexesAsync();

        crawlJobRepository = new CrawlJobRepository(context);
        crawlSourceRepository = new CrawlSourceRepository(context);
        rawPageRepository = new RawPageRepository(context);
        sourceProductRepository = new SourceProductRepository(context);
        canonicalProductRepository = new CanonicalProductRepository(context);
        productOfferRepository = new ProductOfferRepository(context);
        mergeConflictRepository = new MergeConflictRepository(context);
        crawlQueueRepository = new CrawlQueueRepository(context);
        discoveryQueueRepository = new DiscoveryQueueRepository(context);
        discoveredUrlRepository = new DiscoveredUrlRepository(context);
    }

    [Test]
    public async Task CrawlJobRepository_UpsertsAndListsMostRecentJobs()
    {
        await crawlJobRepository.UpsertAsync(new CrawlJob
        {
            JobId = "job-1",
            RequestType = CrawlJobRequestTypes.Category,
            RequestedCategories = ["tv"],
            TotalTargets = 2,
            StartedAt = new DateTime(2026, 03, 20, 10, 00, 00, DateTimeKind.Utc),
            LastUpdatedAt = new DateTime(2026, 03, 20, 10, 05, 00, DateTimeKind.Utc),
            Status = CrawlJobStatuses.Running,
            PerCategoryBreakdown = [new CrawlJobCategoryBreakdown { CategoryKey = "tv", TotalTargets = 2 }]
        });
        await crawlJobRepository.UpsertAsync(new CrawlJob
        {
            JobId = "job-2",
            RequestType = CrawlJobRequestTypes.Source,
            RequestedSources = ["ao"],
            TotalTargets = 1,
            StartedAt = new DateTime(2026, 03, 20, 10, 10, 00, DateTimeKind.Utc),
            LastUpdatedAt = new DateTime(2026, 03, 20, 10, 15, 00, DateTimeKind.Utc),
            Status = CrawlJobStatuses.Pending,
            PerCategoryBreakdown = [new CrawlJobCategoryBreakdown { CategoryKey = "tv", TotalTargets = 1 }]
        });

        var stored = await crawlJobRepository.GetAsync("job-1");
        var listed = await crawlJobRepository.ListAsync(new CrawlJobQuery());

        Assert.Multiple(() =>
        {
            Assert.That(stored, Is.Not.Null);
            Assert.That(stored!.Status, Is.EqualTo(CrawlJobStatuses.Running));
            Assert.That(listed.Items.Select(job => job.JobId), Is.EqualTo(new[] { "job-2", "job-1" }));
        });
    }

    [Test]
    public async Task CrawlSourceRepository_UpsertsListsAndGetsManagedSources()
    {
        var source = new CrawlSource
        {
            Id = "ao_uk",
            DisplayName = "AO UK",
            BaseUrl = "https://ao.com/",
            Host = "ao.com",
            Description = "Managed appliance source.",
            IsEnabled = true,
            SupportedCategoryKeys = ["refrigerator", "tv"],
            ThrottlingPolicy = new SourceThrottlingPolicy
            {
                MinDelayMs = 1500,
                MaxDelayMs = 4500,
                MaxConcurrentRequests = 2,
                RequestsPerMinute = 24,
                RespectRobotsTxt = true
            },
            CreatedUtc = new DateTime(2026, 03, 20, 10, 00, 00, DateTimeKind.Utc),
            UpdatedUtc = new DateTime(2026, 03, 20, 10, 05, 00, DateTimeKind.Utc)
        };

        await crawlSourceRepository.UpsertAsync(source);

        var stored = await crawlSourceRepository.GetAsync("ao_uk");
        var listed = await crawlSourceRepository.ListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(stored, Is.Not.Null);
            Assert.That(stored!.Host, Is.EqualTo("ao.com"));
            Assert.That(stored.ThrottlingPolicy.RequestsPerMinute, Is.EqualTo(24));
            Assert.That(stored.SupportedCategoryKeys, Is.EqualTo(new[] { "refrigerator", "tv" }));
            Assert.That(listed.Select(item => item.Id), Is.EqualTo(new[] { "ao_uk" }));
        });
    }

    [Test]
    public async Task CrawlSourceRepository_ListAsync_SortsByDisplayName()
    {
        await crawlSourceRepository.UpsertAsync(CreateSource("zeta_store", "Zeta Store"));
        await crawlSourceRepository.UpsertAsync(CreateSource("alpha_store", "Alpha Store"));

        var results = await crawlSourceRepository.ListAsync();

        Assert.That(results.Select(source => source.DisplayName), Is.EqualTo(new[] { "Alpha Store", "Zeta Store" }));
    }

    [Test]
    public async Task RawPageRepository_UpsertsAndPreservesHtmlExactly()
    {
        var rawPage = new RawPage
        {
            Id = "page-1",
            SourceName = "example-retailer",
            SourceUrl = "https://example.com/tv/1",
            CategoryKey = "tv",
            Html = "<html><body><script>var x = \"raw\";</script></body></html>",
            StatusCode = 200,
            ContentType = "text/html",
            FetchedUtc = new DateTime(2026, 03, 20, 10, 00, 00, DateTimeKind.Utc)
        };

        await rawPageRepository.UpsertAsync(rawPage);
        var storedPage = await rawPageRepository.GetByIdAsync(rawPage.Id);

        Assert.Multiple(() =>
        {
            Assert.That(storedPage, Is.Not.Null);
            Assert.That(storedPage!.Html, Is.EqualTo(rawPage.Html));
            Assert.That(storedPage.SourceUrl, Is.EqualTo(rawPage.SourceUrl));
        });
    }

    [Test]
    public async Task SourceProductRepository_PreservesRawDataAndSupportsIndexedLookup()
    {
        var sourceProduct = new SourceProduct
        {
            Id = "source-1",
            SourceName = "example-retailer",
            SourceUrl = "https://example.com/tv/1",
            CategoryKey = "tv",
            Brand = "Samsung",
            ModelNumber = "QE55S90D",
            Gtin = "8806095563140",
            Title = "Samsung QE55S90D OLED TV",
            RawSchemaJson = "{\"@type\":\"Product\",\"name\":\"Samsung QE55S90D OLED TV\"}",
            FetchedUtc = new DateTime(2026, 03, 20, 10, 05, 00, DateTimeKind.Utc),
            RawAttributes = new Dictionary<string, SourceAttributeValue>
            {
                ["Screen Size"] = new()
                {
                    AttributeKey = "Screen Size",
                    Value = "55 in",
                    ValueType = "string",
                    SourcePath = "jsonld.additionalProperty"
                }
            }
        };

        await sourceProductRepository.UpsertAsync(sourceProduct);

        var storedProduct = await sourceProductRepository.GetBySourceAsync(sourceProduct.SourceName, sourceProduct.SourceUrl);

        Assert.Multiple(() =>
        {
            Assert.That(storedProduct, Is.Not.Null);
            Assert.That(storedProduct!.RawSchemaJson, Is.EqualTo(sourceProduct.RawSchemaJson));
            Assert.That(storedProduct.RawAttributes["Screen Size"].Value, Is.EqualTo("55 in"));
        });
    }

    [Test]
    public async Task CanonicalProductRepository_RoundTripsAndSupportsLookupIndexes()
    {
        var canonicalProduct = new CanonicalProduct
        {
            Id = "canonical-1",
            CategoryKey = "tv",
            Brand = "Samsung",
            ModelNumber = "QE55S90D",
            Gtin = "8806095563140",
            DisplayName = "Samsung QE55S90D OLED TV",
            CreatedUtc = new DateTime(2026, 03, 20, 10, 10, 00, DateTimeKind.Utc),
            UpdatedUtc = new DateTime(2026, 03, 20, 10, 11, 00, DateTimeKind.Utc),
            Attributes = new Dictionary<string, CanonicalAttributeValue>
            {
                ["screen_size_inch"] = new()
                {
                    AttributeKey = "screen_size_inch",
                    Value = 55m,
                    ValueType = "decimal",
                    Unit = "inch",
                    Confidence = 0.97m
                }
            },
            Sources =
            [
                new ProductSourceLink
                {
                    SourceName = "example-retailer",
                    SourceProductId = "source-1",
                    SourceUrl = "https://example.com/tv/1",
                    FirstSeenUtc = new DateTime(2026, 03, 20, 10, 00, 00, DateTimeKind.Utc),
                    LastSeenUtc = new DateTime(2026, 03, 20, 10, 05, 00, DateTimeKind.Utc)
                }
            ]
        };

        await canonicalProductRepository.UpsertAsync(canonicalProduct);

        var byGtin = await canonicalProductRepository.GetByGtinAsync("8806095563140");
        var byBrandAndModel = await canonicalProductRepository.GetByBrandAndModelAsync("Samsung", "QE55S90D");

        Assert.Multiple(() =>
        {
            Assert.That(byGtin, Is.Not.Null);
            Assert.That(byBrandAndModel, Is.Not.Null);
            Assert.That(byGtin!.Attributes["screen_size_inch"].Value, Is.EqualTo(55m));
            Assert.That(byBrandAndModel!.DisplayName, Is.EqualTo("Samsung QE55S90D OLED TV"));
        });
    }

    [Test]
    public async Task ProductOfferRepository_UpsertsAndQueriesByCanonicalProductId()
    {
        var offer = new ProductOffer
        {
            Id = "offer-1",
            CanonicalProductId = "canonical-1",
            SourceName = "example-retailer",
            SourceUrl = "https://example.com/tv/1",
            Price = 1299.99m,
            Currency = "GBP",
            Availability = "InStock",
            ObservedUtc = new DateTime(2026, 03, 20, 10, 15, 00, DateTimeKind.Utc)
        };

        await productOfferRepository.UpsertAsync(offer);

        var offers = await productOfferRepository.GetByCanonicalProductIdAsync("canonical-1");

        Assert.Multiple(() =>
        {
            Assert.That(offers, Has.Count.EqualTo(1));
            Assert.That(offers[0].Price, Is.EqualTo(1299.99m));
            Assert.That(offers[0].CanonicalProductId, Is.EqualTo("canonical-1"));
        });
    }

    [Test]
    public async Task MergeConflictRepository_UpsertsAndQueriesByCanonicalProductIdAndStatus()
    {
        var conflict = new MergeConflict
        {
            Id = "conflict-1",
            CanonicalProductId = "canonical-1",
            AttributeKey = "screen_size_inch",
            ExistingValue = 55m,
            IncomingValue = 54.6m,
            Reason = "Two sources disagree on the nominal size.",
            Severity = 0.8m,
            Status = "open",
            CreatedUtc = new DateTime(2026, 03, 20, 10, 20, 00, DateTimeKind.Utc)
        };

        await mergeConflictRepository.UpsertAsync(conflict);

        var conflicts = await mergeConflictRepository.GetByCanonicalProductIdAndStatusAsync("canonical-1", "open");

        Assert.Multiple(() =>
        {
            Assert.That(conflicts, Has.Count.EqualTo(1));
            Assert.That(conflicts[0].AttributeKey, Is.EqualTo("screen_size_inch"));
            Assert.That(conflicts[0].ExistingValue, Is.EqualTo(55m));
        });
    }

    [Test]
    public async Task CrawlQueueRepository_UpsertsAndReturnsNextQueuedItem()
    {
        var queuedItem = new CrawlQueueItem
        {
            Id = "queue-1",
            SourceName = "example-retailer",
            SourceUrl = "https://example.com/tv/next",
            CategoryKey = "tv",
            Status = "queued",
            AttemptCount = 0,
            EnqueuedUtc = new DateTime(2026, 03, 20, 10, 25, 00, DateTimeKind.Utc),
            NextAttemptUtc = new DateTime(2026, 03, 20, 10, 30, 00, DateTimeKind.Utc)
        };

        await crawlQueueRepository.UpsertAsync(queuedItem);

        var nextItem = await crawlQueueRepository.GetNextQueuedAsync(new DateTime(2026, 03, 20, 10, 30, 00, DateTimeKind.Utc));

        Assert.Multiple(() =>
        {
            Assert.That(nextItem, Is.Not.Null);
            Assert.That(nextItem!.Id, Is.EqualTo("queue-1"));
            Assert.That(nextItem.SourceUrl, Is.EqualTo("https://example.com/tv/next"));
        });
    }

    [Test]
    public async Task DiscoveredUrlRepository_UpsertsAndFindsByNormalizedUrl()
    {
        var discovered = new DiscoveredUrl
        {
            Id = "discovered-1",
            JobId = "job-1",
            SourceId = "ao_uk",
            CategoryKey = "tv",
            Url = "https://ao.com/tvs/oled/samsung-qe55s90d?ref=nav",
            NormalizedUrl = "https://ao.com/tvs/oled/samsung-qe55s90d",
            Classification = "product",
            State = "processed",
            ParentUrl = "https://ao.com/tvs/oled",
            Depth = 2,
            AttemptCount = 1,
            FirstSeenUtc = new DateTime(2026, 03, 20, 10, 00, 00, DateTimeKind.Utc),
            LastSeenUtc = new DateTime(2026, 03, 20, 10, 05, 00, DateTimeKind.Utc),
            LastProcessedUtc = new DateTime(2026, 03, 20, 10, 06, 00, DateTimeKind.Utc),
            PromotedToCrawlUtc = new DateTime(2026, 03, 20, 10, 06, 30, DateTimeKind.Utc)
        };

        await discoveredUrlRepository.UpsertAsync(discovered);

        var stored = await discoveredUrlRepository.GetByIdAsync(discovered.Id);
        var byNormalizedUrl = await discoveredUrlRepository.GetByNormalizedUrlAsync("ao_uk", "tv", "https://ao.com/tvs/oled/samsung-qe55s90d");

        Assert.Multiple(() =>
        {
            Assert.That(stored, Is.Not.Null);
            Assert.That(stored!.State, Is.EqualTo("processed"));
            Assert.That(stored.NormalizedUrl, Is.EqualTo("https://ao.com/tvs/oled/samsung-qe55s90d"));
            Assert.That(byNormalizedUrl, Is.Not.Null);
            Assert.That(byNormalizedUrl!.Id, Is.EqualTo("discovered-1"));
            Assert.That(byNormalizedUrl.Classification, Is.EqualTo("product"));
        });
    }

    [Test]
    public async Task DiscoveryQueueRepository_AcquiresOnlyDueQueuedItems()
    {
        var queuedItem = new DiscoveryQueueItem
        {
            Id = "discovery-1",
            JobId = "job-1",
            SourceId = "ao_uk",
            CategoryKey = "tv",
            Url = "https://ao.com/tvs/oled?page=2",
            NormalizedUrl = "https://ao.com/tvs/oled?page=2",
            Classification = "listing",
            State = "queued",
            Depth = 1,
            ParentUrl = "https://ao.com/tvs",
            AttemptCount = 0,
            EnqueuedUtc = new DateTime(2026, 03, 20, 10, 00, 00, DateTimeKind.Utc),
            NextAttemptUtc = new DateTime(2026, 03, 20, 10, 02, 00, DateTimeKind.Utc)
        };

        var futureItem = new DiscoveryQueueItem
        {
            Id = "discovery-2",
            JobId = "job-1",
            SourceId = "ao_uk",
            CategoryKey = "tv",
            Url = "https://ao.com/tvs/oled?page=3",
            NormalizedUrl = "https://ao.com/tvs/oled?page=3",
            Classification = "listing",
            State = "queued",
            Depth = 1,
            AttemptCount = 0,
            EnqueuedUtc = new DateTime(2026, 03, 20, 10, 01, 00, DateTimeKind.Utc),
            NextAttemptUtc = new DateTime(2026, 03, 20, 10, 30, 00, DateTimeKind.Utc)
        };

        await discoveryQueueRepository.UpsertAsync(queuedItem);
        await discoveryQueueRepository.UpsertAsync(futureItem);

        var dueItems = await discoveryQueueRepository.ListQueuedAsync(new DateTime(2026, 03, 20, 10, 05, 00, DateTimeKind.Utc));
        var acquired = await discoveryQueueRepository.TryAcquireAsync("discovery-1", new DateTime(2026, 03, 20, 10, 05, 00, DateTimeKind.Utc));
        var reacquired = await discoveryQueueRepository.TryAcquireAsync("discovery-1", new DateTime(2026, 03, 20, 10, 06, 00, DateTimeKind.Utc));
        var notDue = await discoveryQueueRepository.TryAcquireAsync("discovery-2", new DateTime(2026, 03, 20, 10, 05, 00, DateTimeKind.Utc));

        Assert.Multiple(() =>
        {
            Assert.That(dueItems.Select(item => item.Id), Is.EqualTo(new[] { "discovery-1" }));
            Assert.That(acquired, Is.Not.Null);
            Assert.That(acquired!.State, Is.EqualTo("processing"));
            Assert.That(acquired.AttemptCount, Is.EqualTo(1));
            Assert.That(acquired.LastAttemptUtc, Is.EqualTo(new DateTime(2026, 03, 20, 10, 05, 00, DateTimeKind.Utc)));
            Assert.That(acquired.NextAttemptUtc, Is.Null);
            Assert.That(reacquired, Is.Null);
            Assert.That(notDue, Is.Null);
        });
    }

    [Test]
    public async Task MongoDbContext_CreatesRequiredIndexes()
    {
        var context = MongoIntegrationTestFixture.Context;

        var crawlJobIndexes = await context.CrawlJobs.Indexes.ListAsync();
        var crawlSourceIndexes = await context.CrawlSources.Indexes.ListAsync();
        var discoveryQueueIndexes = await context.DiscoveryQueueItems.Indexes.ListAsync();
        var discoveredUrlIndexes = await context.DiscoveredUrls.Indexes.ListAsync();
        var canonicalIndexes = await context.CanonicalProducts.Indexes.ListAsync();
        var sourceIndexes = await context.SourceProducts.Indexes.ListAsync();
        var offerIndexes = await context.ProductOffers.Indexes.ListAsync();
        var conflictIndexes = await context.MergeConflicts.Indexes.ListAsync();

        var crawlJobIndexDefinitions = await crawlJobIndexes.ToListAsync();
        var crawlSourceIndexDefinitions = await crawlSourceIndexes.ToListAsync();
        var discoveryQueueIndexDefinitions = await discoveryQueueIndexes.ToListAsync();
        var discoveredUrlIndexDefinitions = await discoveredUrlIndexes.ToListAsync();
        var canonicalIndexDefinitions = await canonicalIndexes.ToListAsync();
        var sourceIndexDefinitions = await sourceIndexes.ToListAsync();
        var offerIndexDefinitions = await offerIndexes.ToListAsync();
        var conflictIndexDefinitions = await conflictIndexes.ToListAsync();

        var discoveredUrlUniqueIndex = discoveredUrlIndexDefinitions.FirstOrDefault(definition =>
        {
            var name = definition["name"].AsString;
            return !string.Equals(name, "_id_", StringComparison.Ordinal);
        });

        Assert.Multiple(() =>
        {
            Assert.That(crawlJobIndexDefinitions.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(crawlSourceIndexDefinitions.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(discoveryQueueIndexDefinitions.Count, Is.GreaterThanOrEqualTo(4));
            Assert.That(discoveredUrlIndexDefinitions.Count, Is.GreaterThanOrEqualTo(4));
            Assert.That(discoveredUrlUniqueIndex, Is.Not.Null);
            Assert.That(discoveredUrlUniqueIndex!["unique"].AsBoolean, Is.True);
            Assert.That(canonicalIndexDefinitions.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(sourceIndexDefinitions.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(offerIndexDefinitions.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(conflictIndexDefinitions.Count, Is.GreaterThanOrEqualTo(2));
        });
    }

    private static CrawlSource CreateSource(string id, string displayName)
    {
        return new CrawlSource
        {
            Id = id,
            DisplayName = displayName,
            BaseUrl = $"https://{id}.example/",
            Host = $"{id}.example",
            IsEnabled = true,
            SupportedCategoryKeys = ["tv"],
            ThrottlingPolicy = new SourceThrottlingPolicy
            {
                MinDelayMs = 1000,
                MaxDelayMs = 3000,
                MaxConcurrentRequests = 1,
                RequestsPerMinute = 30,
                RespectRobotsTxt = true
            },
            CreatedUtc = new DateTime(2026, 03, 20, 10, 00, 00, DateTimeKind.Utc),
            UpdatedUtc = new DateTime(2026, 03, 20, 10, 00, 00, DateTimeKind.Utc)
        };
    }
}