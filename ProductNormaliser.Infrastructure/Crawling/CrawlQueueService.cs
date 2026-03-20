using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Mongo.Repositories;

namespace ProductNormaliser.Infrastructure.Crawling;

public sealed class CrawlQueueService(ICrawlQueueStore crawlQueueStore) : ICrawlQueueService
{
    public async Task<CrawlQueueLease?> DequeueAsync(CancellationToken cancellationToken)
    {
        var queueItem = await crawlQueueStore.GetNextQueuedAsync(DateTime.UtcNow, cancellationToken);
        if (queueItem is null)
        {
            return null;
        }

        queueItem.Status = "processing";
        queueItem.AttemptCount += 1;
        queueItem.NextAttemptUtc = null;
        queueItem.LastError = null;
        await crawlQueueStore.UpsertAsync(queueItem, cancellationToken);

        return new CrawlQueueLease
        {
            QueueItemId = queueItem.Id,
            Target = new CrawlTarget
            {
                Url = queueItem.SourceUrl,
                CategoryKey = queueItem.CategoryKey,
                Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sourceName"] = queueItem.SourceName,
                    ["queueItemId"] = queueItem.Id
                }
            }
        };
    }

    public async Task MarkCompletedAsync(string queueItemId, CancellationToken cancellationToken)
    {
        var queueItem = await crawlQueueStore.GetByIdAsync(queueItemId, cancellationToken);
        if (queueItem is null)
        {
            return;
        }

        queueItem.Status = "completed";
        queueItem.LastError = null;
        await crawlQueueStore.UpsertAsync(queueItem, cancellationToken);
    }

    public async Task MarkSkippedAsync(string queueItemId, string reason, CancellationToken cancellationToken)
    {
        var queueItem = await crawlQueueStore.GetByIdAsync(queueItemId, cancellationToken);
        if (queueItem is null)
        {
            return;
        }

        queueItem.Status = "skipped";
        queueItem.LastError = reason;
        await crawlQueueStore.UpsertAsync(queueItem, cancellationToken);
    }

    public async Task MarkFailedAsync(string queueItemId, string reason, CancellationToken cancellationToken)
    {
        var queueItem = await crawlQueueStore.GetByIdAsync(queueItemId, cancellationToken);
        if (queueItem is null)
        {
            return;
        }

        queueItem.Status = "failed";
        queueItem.LastError = reason;
        await crawlQueueStore.UpsertAsync(queueItem, cancellationToken);
    }
}