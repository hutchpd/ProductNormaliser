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
        AnalystNotes = Database.GetCollection<AnalystNote>(MongoCollectionNames.AnalystNotes);
        AnalystWorkflows = Database.GetCollection<AnalystWorkflow>(MongoCollectionNames.AnalystWorkflows);
        Categories = Database.GetCollection<CategoryMetadata>(MongoCollectionNames.Categories);
        CrawlJobs = Database.GetCollection<CrawlJob>(MongoCollectionNames.CrawlJobs);
        CrawlSources = Database.GetCollection<CrawlSource>(MongoCollectionNames.CrawlSources);
        DiscoveryRuns = Database.GetCollection<DiscoveryRun>(MongoCollectionNames.DiscoveryRuns);
        DiscoveryRunCandidates = Database.GetCollection<DiscoveryRunCandidate>(MongoCollectionNames.DiscoveryRunCandidates);
        DiscoveryRunCandidateDispositions = Database.GetCollection<DiscoveryRunCandidateDisposition>(MongoCollectionNames.DiscoveryRunCandidateDispositions);
        DiscoveryQueueItems = Database.GetCollection<DiscoveryQueueItem>(MongoCollectionNames.DiscoveryQueue);
        DiscoveredUrls = Database.GetCollection<DiscoveredUrl>(MongoCollectionNames.DiscoveredUrls);
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

    public IMongoCollection<AnalystNote> AnalystNotes { get; }

    public IMongoCollection<AnalystWorkflow> AnalystWorkflows { get; }

    public IMongoCollection<CategoryMetadata> Categories { get; }

    public IMongoCollection<CrawlJob> CrawlJobs { get; }

    public IMongoCollection<CrawlSource> CrawlSources { get; }

    public IMongoCollection<DiscoveryRun> DiscoveryRuns { get; }

    public IMongoCollection<DiscoveryRunCandidate> DiscoveryRunCandidates { get; }

    public IMongoCollection<DiscoveryRunCandidateDisposition> DiscoveryRunCandidateDispositions { get; }

    public IMongoCollection<DiscoveryQueueItem> DiscoveryQueueItems { get; }

    public IMongoCollection<DiscoveredUrl> DiscoveredUrls { get; }

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
        await MongoIndexCatalog.EnsureAsync(this, cancellationToken);
    }
}