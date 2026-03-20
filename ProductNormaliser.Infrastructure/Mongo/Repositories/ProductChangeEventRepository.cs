using MongoDB.Driver;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class ProductChangeEventRepository(MongoDbContext context)
    : MongoRepositoryBase<ProductChangeEvent>(context.ProductChangeEvents), IProductChangeEventStore
{
    public async Task InsertManyAsync(IReadOnlyCollection<ProductChangeEvent> changeEvents, CancellationToken cancellationToken = default)
    {
        if (changeEvents.Count == 0)
        {
            return;
        }

        await Collection.InsertManyAsync(changeEvents, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ProductChangeEvent>> GetByCanonicalProductIdAsync(string canonicalProductId, int limit = 100, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(changeEvent => changeEvent.CanonicalProductId == canonicalProductId)
            .SortByDescending(changeEvent => changeEvent.TimestampUtc)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductChangeEvent>> ListByCategoryAsync(string categoryKey, int limit = 500, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(changeEvent => changeEvent.CategoryKey == categoryKey)
            .SortByDescending(changeEvent => changeEvent.TimestampUtc)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }
}