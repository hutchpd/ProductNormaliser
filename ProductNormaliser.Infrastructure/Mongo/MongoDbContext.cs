using MongoDB.Driver;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo;

public sealed class MongoDbContext
{
    public MongoDbContext(MongoSettings settings)
        : this(new MongoClient(settings.ConnectionString), settings.DatabaseName)
    {
    }

    public MongoDbContext(IMongoClient mongoClient, string databaseName)
    {
        ArgumentNullException.ThrowIfNull(mongoClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        MongoMappingRegistry.Register();

        Client = mongoClient;
        Database = mongoClient.GetDatabase(databaseName);
        Categories = Database.GetCollection<CategoryMetadata>(MongoCollectionNames.Categories);
        CrawlJobs = Database.GetCollection<CrawlJob>(MongoCollectionNames.CrawlJobs);
        CrawlSources = Database.GetCollection<CrawlSource>(MongoCollectionNames.CrawlSources);
        RawPages = Database.GetCollection<RawPage>(MongoCollectionNames.RawPages);
        SourceProducts = Database.GetCollection<SourceProduct>(MongoCollectionNames.SourceProducts);
        CanonicalProducts = Database.GetCollection<CanonicalProduct>(MongoCollectionNames.CanonicalProducts);
        ProductOffers = Database.GetCollection<ProductOffer>(MongoCollectionNames.ProductOffers);
        MergeConflicts = Database.GetCollection<MergeConflict>(MongoCollectionNames.MergeConflicts);
        CrawlQueueItems = Database.GetCollection<CrawlQueueItem>(MongoCollectionNames.CrawlQueue);
        CrawlLogs = Database.GetCollection<CrawlLog>(MongoCollectionNames.CrawlLogs);
        UnmappedAttributes = Database.GetCollection<UnmappedAttribute>(MongoCollectionNames.UnmappedAttributes);
        SourceQualitySnapshots = Database.GetCollection<SourceQualitySnapshot>(MongoCollectionNames.SourceQualitySnapshots);
        ProductChangeEvents = Database.GetCollection<ProductChangeEvent>(MongoCollectionNames.ProductChangeEvents);
        AdaptiveCrawlPolicies = Database.GetCollection<AdaptiveCrawlPolicy>(MongoCollectionNames.AdaptiveCrawlPolicies);
        SourceAttributeDisagreements = Database.GetCollection<SourceAttributeDisagreement>(MongoCollectionNames.SourceAttributeDisagreements);
        ManagementAuditEntries = Database.GetCollection<ManagementAuditEntry>(MongoCollectionNames.ManagementAuditEntries);
    }

    public IMongoClient Client { get; }

    public IMongoDatabase Database { get; }

    public IMongoCollection<CategoryMetadata> Categories { get; }

    public IMongoCollection<CrawlJob> CrawlJobs { get; }

    public IMongoCollection<CrawlSource> CrawlSources { get; }

    public IMongoCollection<RawPage> RawPages { get; }

    public IMongoCollection<SourceProduct> SourceProducts { get; }

    public IMongoCollection<CanonicalProduct> CanonicalProducts { get; }

    public IMongoCollection<ProductOffer> ProductOffers { get; }

    public IMongoCollection<MergeConflict> MergeConflicts { get; }

    public IMongoCollection<CrawlQueueItem> CrawlQueueItems { get; }

    public IMongoCollection<CrawlLog> CrawlLogs { get; }

    public IMongoCollection<UnmappedAttribute> UnmappedAttributes { get; }

    public IMongoCollection<SourceQualitySnapshot> SourceQualitySnapshots { get; }

    public IMongoCollection<ProductChangeEvent> ProductChangeEvents { get; }

    public IMongoCollection<AdaptiveCrawlPolicy> AdaptiveCrawlPolicies { get; }

    public IMongoCollection<SourceAttributeDisagreement> SourceAttributeDisagreements { get; }

    public IMongoCollection<ManagementAuditEntry> ManagementAuditEntries { get; }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        await Categories.Indexes.CreateOneAsync(
            new CreateIndexModel<CategoryMetadata>(Builders<CategoryMetadata>.IndexKeys
                .Ascending(category => category.FamilyKey)
                .Ascending(category => category.IsEnabled)
                .Ascending(category => category.CrawlSupportStatus)),
            cancellationToken: cancellationToken);

        await CrawlJobs.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<CrawlJob>(Builders<CrawlJob>.IndexKeys
                    .Ascending(job => job.Status)
                    .Descending(job => job.LastUpdatedAt)),
                new CreateIndexModel<CrawlJob>(Builders<CrawlJob>.IndexKeys
                    .Descending(job => job.StartedAt))
            ],
            cancellationToken: cancellationToken);

        await CrawlSources.Indexes.CreateOneAsync(
            new CreateIndexModel<CrawlSource>(Builders<CrawlSource>.IndexKeys
                .Ascending(source => source.Host)
                .Ascending(source => source.IsEnabled)),
            cancellationToken: cancellationToken);

        await CanonicalProducts.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<CanonicalProduct>(Builders<CanonicalProduct>.IndexKeys.Ascending(product => product.Gtin)),
                new CreateIndexModel<CanonicalProduct>(Builders<CanonicalProduct>.IndexKeys
                    .Ascending(product => product.Brand)
                    .Ascending(product => product.ModelNumber))
            ],
            cancellationToken: cancellationToken);

        await SourceProducts.Indexes.CreateOneAsync(
            new CreateIndexModel<SourceProduct>(Builders<SourceProduct>.IndexKeys
                .Ascending(product => product.SourceName)
                .Ascending(product => product.SourceUrl)),
            cancellationToken: cancellationToken);

        await ProductOffers.Indexes.CreateOneAsync(
            new CreateIndexModel<ProductOffer>(Builders<ProductOffer>.IndexKeys.Ascending(offer => offer.CanonicalProductId)),
            cancellationToken: cancellationToken);

        await MergeConflicts.Indexes.CreateOneAsync(
            new CreateIndexModel<MergeConflict>(Builders<MergeConflict>.IndexKeys
                .Ascending(conflict => conflict.CanonicalProductId)
                .Ascending(conflict => conflict.Status)),
            cancellationToken: cancellationToken);

        await UnmappedAttributes.Indexes.CreateOneAsync(
            new CreateIndexModel<UnmappedAttribute>(Builders<UnmappedAttribute>.IndexKeys
                .Ascending(attribute => attribute.CategoryKey)
                .Ascending(attribute => attribute.OccurrenceCount)),
            cancellationToken: cancellationToken);

        await SourceQualitySnapshots.Indexes.CreateOneAsync(
            new CreateIndexModel<SourceQualitySnapshot>(Builders<SourceQualitySnapshot>.IndexKeys
                .Ascending(snapshot => snapshot.SourceName)
                .Ascending(snapshot => snapshot.CategoryKey)
                .Descending(snapshot => snapshot.TimestampUtc)),
            cancellationToken: cancellationToken);

        await ProductChangeEvents.Indexes.CreateOneAsync(
            new CreateIndexModel<ProductChangeEvent>(Builders<ProductChangeEvent>.IndexKeys
                .Ascending(changeEvent => changeEvent.CanonicalProductId)
                .Descending(changeEvent => changeEvent.TimestampUtc)),
            cancellationToken: cancellationToken);

        await AdaptiveCrawlPolicies.Indexes.CreateOneAsync(
            new CreateIndexModel<AdaptiveCrawlPolicy>(Builders<AdaptiveCrawlPolicy>.IndexKeys
                .Ascending(policy => policy.SourceName)
                .Ascending(policy => policy.CategoryKey)),
            cancellationToken: cancellationToken);

        await SourceAttributeDisagreements.Indexes.CreateOneAsync(
            new CreateIndexModel<SourceAttributeDisagreement>(Builders<SourceAttributeDisagreement>.IndexKeys
                .Ascending(disagreement => disagreement.SourceName)
                .Ascending(disagreement => disagreement.CategoryKey)
                .Ascending(disagreement => disagreement.AttributeKey)),
            cancellationToken: cancellationToken);

        await ManagementAuditEntries.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<ManagementAuditEntry>(Builders<ManagementAuditEntry>.IndexKeys
                    .Descending(entry => entry.TimestampUtc)),
                new CreateIndexModel<ManagementAuditEntry>(Builders<ManagementAuditEntry>.IndexKeys
                    .Ascending(entry => entry.TargetType)
                    .Ascending(entry => entry.TargetId)
                    .Descending(entry => entry.TimestampUtc))
            ],
            cancellationToken: cancellationToken);
    }
}