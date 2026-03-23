using MongoDB.Driver;
using ProductNormaliser.Application.Crawls;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class CrawlQueueRepository(MongoDbContext context) : MongoRepositoryBase<CrawlQueueItem>(context.CrawlQueueItems), ICrawlQueueStore, ICrawlJobQueueWriter
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

    public async Task<CrawlQueueItem?> TryAcquireAsync(string id, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var filter = Builders<CrawlQueueItem>.Filter.Where(item =>
            item.Id == id
            && item.Status == "queued"
            && (item.NextAttemptUtc == null || item.NextAttemptUtc <= utcNow));
        var update = Builders<CrawlQueueItem>.Update
            .Set(item => item.Status, "processing")
            .Inc(item => item.AttemptCount, 1)
            .Set(item => item.LastAttemptUtc, utcNow)
            .Set(item => item.NextAttemptUtc, (DateTime?)null)
            .Set(item => item.LastError, null);

        return await Collection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<CrawlQueueItem>
            {
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<CrawlQueueItem>> CancelQueuedItemsAsync(string jobId, string reason, CancellationToken cancellationToken = default)
    {
        var queuedItems = await Collection.Find(item => item.JobId == jobId && item.Status == "queued")
            .ToListAsync(cancellationToken);

        if (queuedItems.Count == 0)
        {
            return [];
        }

        foreach (var item in queuedItems)
        {
            item.Status = "cancelled";
            item.LastError = reason;
            item.NextAttemptUtc = null;
        }

        foreach (var item in queuedItems)
        {
            await UpsertAsync(item, cancellationToken);
        }

        return queuedItems;
    }

    public async Task<CrawlQueueItem?> GetNextQueuedAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(item => item.Status == "queued" && (item.NextAttemptUtc == null || item.NextAttemptUtc <= utcNow))
            .SortBy(item => item.EnqueuedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CrawlQueueItem>> ListQueuedAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(item => item.Status == "queued" && (item.NextAttemptUtc == null || item.NextAttemptUtc <= utcNow))
            .SortBy(item => item.EnqueuedUtc)
            .ToListAsync(cancellationToken);
    }
}