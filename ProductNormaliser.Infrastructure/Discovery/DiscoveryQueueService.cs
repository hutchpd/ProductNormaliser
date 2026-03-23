using ProductNormaliser.Application.Discovery;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Mongo.Repositories;

namespace ProductNormaliser.Infrastructure.Discovery;

public sealed class DiscoveryQueueService(
    IDiscoveryQueueStore discoveryQueueStore,
    IDiscoveredUrlStore discoveredUrlStore,
    ProductTargetEnqueuer productTargetEnqueuer,
    DiscoveryJobProgressService discoveryJobProgressService) : IDiscoveryQueueService, IDiscoverySeedWriter
{
    public async Task<DiscoveryQueueLease?> DequeueAsync(CancellationToken cancellationToken)
    {
        var queuedItems = await discoveryQueueStore.ListQueuedAsync(DateTime.UtcNow, cancellationToken);
        foreach (var item in queuedItems)
        {
            var acquired = await discoveryQueueStore.TryAcquireAsync(item.Id, DateTime.UtcNow, cancellationToken);
            if (acquired is not null)
            {
                return new DiscoveryQueueLease
                {
                    QueueItemId = acquired.Id,
                    Item = acquired
                };
            }
        }

        return null;
    }

    public async Task<bool> EnqueueAsync(CrawlSource source, string categoryKey, string url, string itemType, int depth, string? parentUrl, string? jobId, CancellationToken cancellationToken)
    {
        var normalizedUrl = DiscoveryIdentity.NormalizeUrl(url);
        var queueId = DiscoveryIdentity.BuildDiscoveryQueueId(source.Id, categoryKey, url, itemType);
        var existingQueueItem = await discoveryQueueStore.GetByIdAsync(queueId, cancellationToken);
        if (existingQueueItem is not null)
        {
            if (string.IsNullOrWhiteSpace(existingQueueItem.JobId) && !string.IsNullOrWhiteSpace(jobId))
            {
                existingQueueItem.JobId = jobId;
                await discoveryQueueStore.UpsertAsync(existingQueueItem, cancellationToken);
            }

            await RecordDiscoveredUrlAsync(jobId, source.Id, categoryKey, url, normalizedUrl, itemType, "pending", depth, parentUrl, promotedToCrawlUtc: null, nextAttemptUtc: DateTime.UtcNow, lastError: null, cancellationToken);
            return false;
        }

        var now = DateTime.UtcNow;
        await discoveryQueueStore.UpsertAsync(new DiscoveryQueueItem
        {
            Id = queueId,
            JobId = jobId,
            SourceId = source.Id,
            CategoryKey = categoryKey,
            Url = url,
            NormalizedUrl = normalizedUrl,
            Classification = itemType,
            State = "queued",
            Depth = depth,
            ParentUrl = parentUrl,
            AttemptCount = 0,
            EnqueuedUtc = now,
            NextAttemptUtc = now
        }, cancellationToken);

        await RecordDiscoveredUrlAsync(jobId, source.Id, categoryKey, url, normalizedUrl, itemType, "pending", depth, parentUrl, promotedToCrawlUtc: null, nextAttemptUtc: now, lastError: null, cancellationToken);
        return true;
    }

    public async Task<bool> EnqueueProductAsync(CrawlSource source, string categoryKey, string url, int depth, string? parentUrl, string? jobId, CancellationToken cancellationToken)
    {
        var normalizedUrl = DiscoveryIdentity.NormalizeUrl(url);
        var now = DateTime.UtcNow;
        var enqueued = await productTargetEnqueuer.EnqueueAsync(jobId, source, categoryKey, url, cancellationToken);
        await RecordDiscoveredUrlAsync(jobId, source.Id, categoryKey, url, normalizedUrl, "product", "pending", depth, parentUrl, now, nextAttemptUtc: now, lastError: null, cancellationToken);
        return enqueued;
    }

    public async Task MarkCompletedAsync(string queueItemId, CancellationToken cancellationToken)
    {
        var existing = await discoveryQueueStore.GetByIdAsync(queueItemId, cancellationToken);
        if (existing is null)
        {
            return;
        }

        existing.State = "completed";
        existing.CompletedUtc = DateTime.UtcNow;
        existing.LastError = null;
        await discoveryQueueStore.UpsertAsync(existing, cancellationToken);
        await UpdateDiscoveredUrlStateAsync(existing, "processed", lastError: null, cancellationToken);
        await discoveryJobProgressService.RecordProcessedPageAsync(existing.JobId, existing.CategoryKey, "completed", cancellationToken);
    }

    public async Task MarkSkippedAsync(string queueItemId, string reason, CancellationToken cancellationToken)
    {
        var existing = await discoveryQueueStore.GetByIdAsync(queueItemId, cancellationToken);
        if (existing is null)
        {
            return;
        }

        existing.State = "skipped";
        existing.CompletedUtc = DateTime.UtcNow;
        existing.LastError = reason;
        await discoveryQueueStore.UpsertAsync(existing, cancellationToken);
        var discoveredState = reason.Contains("robots", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("blocked", StringComparison.OrdinalIgnoreCase)
            ? "blocked"
            : "skipped";
        await UpdateDiscoveredUrlStateAsync(existing, discoveredState, reason, cancellationToken);
        await discoveryJobProgressService.RecordProcessedPageAsync(existing.JobId, existing.CategoryKey, discoveredState, cancellationToken);
    }

    public async Task MarkFailedAsync(string queueItemId, string reason, CancellationToken cancellationToken)
    {
        var existing = await discoveryQueueStore.GetByIdAsync(queueItemId, cancellationToken);
        if (existing is null)
        {
            return;
        }

        existing.State = "failed";
        existing.CompletedUtc = DateTime.UtcNow;
        existing.LastError = reason;
        await discoveryQueueStore.UpsertAsync(existing, cancellationToken);
        await UpdateDiscoveredUrlStateAsync(existing, "rejected", reason, cancellationToken);
        await discoveryJobProgressService.RecordProcessedPageAsync(existing.JobId, existing.CategoryKey, "rejected", cancellationToken);
    }

    private async Task RecordDiscoveredUrlAsync(
        string? jobId,
        string sourceId,
        string categoryKey,
        string url,
        string normalizedUrl,
        string classification,
        string state,
        int depth,
        string? parentUrl,
        DateTime? promotedToCrawlUtc,
        DateTime? nextAttemptUtc,
        string? lastError,
        CancellationToken cancellationToken)
    {
        var id = DiscoveryIdentity.BuildDiscoveredUrlId(sourceId, categoryKey, url);
        var now = DateTime.UtcNow;
        var existing = await discoveredUrlStore.GetByNormalizedUrlAsync(sourceId, categoryKey, normalizedUrl, cancellationToken);
        var isNew = existing is null;

        await discoveredUrlStore.UpsertAsync(new DiscoveredUrl
        {
            Id = existing?.Id ?? id,
            JobId = existing?.JobId ?? jobId,
            SourceId = sourceId,
            CategoryKey = categoryKey,
            Url = url,
            NormalizedUrl = normalizedUrl,
            Classification = classification,
            State = state,
            ParentUrl = existing?.ParentUrl ?? parentUrl,
            Depth = existing is null ? depth : Math.Min(existing.Depth, depth),
            AttemptCount = existing?.AttemptCount ?? 0,
            FirstSeenUtc = existing?.FirstSeenUtc ?? now,
            LastSeenUtc = now,
            LastProcessedUtc = existing?.LastProcessedUtc,
            NextAttemptUtc = nextAttemptUtc ?? existing?.NextAttemptUtc,
            PromotedToCrawlUtc = promotedToCrawlUtc ?? existing?.PromotedToCrawlUtc,
            LastError = lastError ?? existing?.LastError
        }, cancellationToken);

        if (isNew)
        {
            await discoveryJobProgressService.RecordDiscoveredUrlAsync(jobId, categoryKey, cancellationToken);
        }
    }

    private async Task UpdateDiscoveredUrlStateAsync(DiscoveryQueueItem queueItem, string state, string? lastError, CancellationToken cancellationToken)
    {
        var existing = await discoveredUrlStore.GetByNormalizedUrlAsync(queueItem.SourceId, queueItem.CategoryKey, queueItem.NormalizedUrl, cancellationToken);
        if (existing is null)
        {
            return;
        }

        existing.State = state;
        existing.AttemptCount = Math.Max(existing.AttemptCount, queueItem.AttemptCount);
        existing.LastProcessedUtc = DateTime.UtcNow;
        existing.NextAttemptUtc = null;
        existing.LastError = lastError;
        await discoveredUrlStore.UpsertAsync(existing, cancellationToken);
    }
}