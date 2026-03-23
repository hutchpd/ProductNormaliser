using MongoDB.Driver;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class DiscoveryQueueRepository(MongoDbContext context)
    : MongoRepositoryBase<DiscoveryQueueItem>(context.DiscoveryQueueItems), IDiscoveryQueueStore
{
    public async Task<DiscoveryQueueItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(item => item.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertAsync(DiscoveryQueueItem item, CancellationToken cancellationToken = default)
    {
        await Collection.ReplaceOneAsync(
            existing => existing.Id == item.Id,
            item,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<DiscoveryQueueItem?> TryAcquireAsync(string id, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var filter = Builders<DiscoveryQueueItem>.Filter.Where(item => item.Id == id && item.State == "queued" && (item.NextAttemptUtc == null || item.NextAttemptUtc <= utcNow));
        var update = Builders<DiscoveryQueueItem>.Update
            .Set(item => item.State, "processing")
            .Inc(item => item.AttemptCount, 1)
            .Set(item => item.LastAttemptUtc, utcNow)
            .Set(item => item.NextAttemptUtc, (DateTime?)null)
            .Set(item => item.LastError, null);

        return await Collection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<DiscoveryQueueItem>
            {
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<DiscoveryQueueItem>> ListQueuedAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(item => item.State == "queued" && (item.NextAttemptUtc == null || item.NextAttemptUtc <= utcNow))
            .SortBy(item => item.NextAttemptUtc)
            .ThenBy(item => item.EnqueuedUtc)
            .ToListAsync(cancellationToken);
    }
}