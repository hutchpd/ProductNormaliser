using MongoDB.Driver;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class CrawlQueueRepository(MongoDbContext context) : MongoRepositoryBase<CrawlQueueItem>(context.CrawlQueueItems), ICrawlQueueStore
{
    public async Task<CrawlQueueItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(item => item.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertAsync(CrawlQueueItem item, CancellationToken cancellationToken = default)
    {
        await Collection.ReplaceOneAsync(
            existingItem => existingItem.Id == item.Id,
            item,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<CrawlQueueItem?> GetNextQueuedAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(item => item.Status == "queued" && (item.NextAttemptUtc == null || item.NextAttemptUtc <= utcNow))
            .SortBy(item => item.EnqueuedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }
}