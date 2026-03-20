using MongoDB.Driver;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public abstract class MongoRepositoryBase<TDocument>(IMongoCollection<TDocument> collection)
{
    protected IMongoCollection<TDocument> Collection { get; } = collection;

    public virtual async Task InsertAsync(TDocument document, CancellationToken cancellationToken = default)
    {
        await Collection.InsertOneAsync(document, cancellationToken: cancellationToken);
    }

    public virtual async Task<IReadOnlyList<TDocument>> ListAsync(FilterDefinition<TDocument>? filter = null, CancellationToken cancellationToken = default)
    {
        var cursor = await Collection.FindAsync(filter ?? Builders<TDocument>.Filter.Empty, cancellationToken: cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }
}