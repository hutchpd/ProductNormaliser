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
        RawPages = Database.GetCollection<RawPage>(MongoCollectionNames.RawPages);
        SourceProducts = Database.GetCollection<SourceProduct>(MongoCollectionNames.SourceProducts);
        CanonicalProducts = Database.GetCollection<CanonicalProduct>(MongoCollectionNames.CanonicalProducts);
        ProductOffers = Database.GetCollection<ProductOffer>(MongoCollectionNames.ProductOffers);
        MergeConflicts = Database.GetCollection<MergeConflict>(MongoCollectionNames.MergeConflicts);
        CrawlQueueItems = Database.GetCollection<CrawlQueueItem>(MongoCollectionNames.CrawlQueue);
    }

    public IMongoClient Client { get; }

    public IMongoDatabase Database { get; }

    public IMongoCollection<RawPage> RawPages { get; }

    public IMongoCollection<SourceProduct> SourceProducts { get; }

    public IMongoCollection<CanonicalProduct> CanonicalProducts { get; }

    public IMongoCollection<ProductOffer> ProductOffers { get; }

    public IMongoCollection<MergeConflict> MergeConflicts { get; }

    public IMongoCollection<CrawlQueueItem> CrawlQueueItems { get; }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
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
    }
}