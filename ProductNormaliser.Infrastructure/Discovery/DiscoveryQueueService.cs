using ProductNormaliser.Application.Discovery;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Mongo.Repositories;

namespace ProductNormaliser.Infrastructure.Discovery;

public sealed class DiscoveryQueueService(
    IDiscoveryQueueStore discoveryQueueStore,
    IDiscoveredUrlStore discoveredUrlStore,
    ICrawlSourceStore crawlSourceStore,
    DiscoveryLinkPolicy discoveryLinkPolicy,
    ProductTargetEnqueuer productTargetEnqueuer,
    DiscoveryJobProgressService discoveryJobProgressService) : IDiscoveryQueueService, IDiscoverySeedWriter
{
    private static readonly TimeSpan DefaultSeedReseedInterval = TimeSpan.FromHours(24);

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
        var now = DateTime.UtcNow;
        if (!discoveryLinkPolicy.TryNormalizeAndValidate(source, categoryKey, url, depth, out var normalizedUrl))
        {
            return false;
        }

        if (!await HasCapacityAsync(source, categoryKey, jobId, cancellationToken))
        {
            return false;
        }

        var queueId = DiscoveryIdentity.BuildDiscoveryQueueId(source.Id, categoryKey, url, itemType);
        var existingQueueItem = await discoveryQueueStore.GetByIdAsync(queueId, cancellationToken);

        if (existingQueueItem is not null)
        {
            if (ShouldRequeueCompletedSeed(existingQueueItem, source.DiscoveryProfile, now))
            {
                existingQueueItem.JobId = jobId;
                existingQueueItem.Url = url;
                existingQueueItem.NormalizedUrl = normalizedUrl;
                existingQueueItem.Classification = itemType;
                existingQueueItem.State = "queued";
                existingQueueItem.Depth = depth;
                existingQueueItem.ParentUrl = parentUrl;
                existingQueueItem.AttemptCount = 0;
                existingQueueItem.EnqueuedUtc = now;
                existingQueueItem.LastAttemptUtc = null;
                existingQueueItem.NextAttemptUtc = now;
                existingQueueItem.CompletedUtc = null;
                existingQueueItem.LastError = null;
                await discoveryQueueStore.UpsertAsync(existingQueueItem, cancellationToken);

                await RecordDiscoveredUrlAsync(jobId, source.Id, categoryKey, url, normalizedUrl, itemType, "pending", depth, parentUrl, promotedToCrawlUtc: null, nextAttemptUtc: now, lastError: null, countForJob: !string.IsNullOrWhiteSpace(jobId), cancellationToken);
                return true;
            }

            var shouldCountForJob = string.IsNullOrWhiteSpace(existingQueueItem.JobId) && !string.IsNullOrWhiteSpace(jobId);
            if (shouldCountForJob)
            {
                existingQueueItem.JobId = jobId;
                await discoveryQueueStore.UpsertAsync(existingQueueItem, cancellationToken);
            }

            var discoveredState = MapDiscoveredState(existingQueueItem);
            DateTime? nextAttemptUtc = existingQueueItem.State == "queued" ? existingQueueItem.NextAttemptUtc ?? now : null;
            await RecordDiscoveredUrlAsync(jobId, source.Id, categoryKey, url, normalizedUrl, itemType, discoveredState, depth, parentUrl, promotedToCrawlUtc: null, nextAttemptUtc, existingQueueItem.LastError, shouldCountForJob, cancellationToken);
            return false;
        }

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

        await RecordDiscoveredUrlAsync(jobId, source.Id, categoryKey, url, normalizedUrl, itemType, "pending", depth, parentUrl, promotedToCrawlUtc: null, nextAttemptUtc: now, lastError: null, countForJob: !string.IsNullOrWhiteSpace(jobId), cancellationToken);
        return true;
    }

    public async Task<bool> EnqueueProductAsync(CrawlSource source, string categoryKey, string url, int depth, string? parentUrl, string? jobId, CancellationToken cancellationToken)
    {
        if (!discoveryLinkPolicy.TryNormalizeAndValidate(source, categoryKey, url, depth, out var normalizedUrl))
        {
            return false;
        }

        if (!await HasCapacityAsync(source, categoryKey, jobId, cancellationToken))
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var enqueued = await productTargetEnqueuer.EnqueueAsync(jobId, source, categoryKey, url, cancellationToken);
        await RecordDiscoveredUrlAsync(jobId, source.Id, categoryKey, url, normalizedUrl, "product", "pending", depth, parentUrl, now, nextAttemptUtc: now, lastError: null, countForJob: false, cancellationToken);
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

        var source = await crawlSourceStore.GetAsync(existing.SourceId, cancellationToken);
        if (source is not null && ShouldRetry(existing, source.DiscoveryProfile))
        {
            existing.State = "queued";
            existing.CompletedUtc = null;
            existing.LastError = reason;
            existing.NextAttemptUtc = ComputeNextAttemptUtc(existing, source.DiscoveryProfile);
            await discoveryQueueStore.UpsertAsync(existing, cancellationToken);
            await UpdateDiscoveredUrlRetryStateAsync(existing, reason, cancellationToken);
            return;
        }

        existing.State = "failed";
        existing.CompletedUtc = DateTime.UtcNow;
        existing.LastError = reason;
        existing.NextAttemptUtc = null;
        await discoveryQueueStore.UpsertAsync(existing, cancellationToken);
        await UpdateDiscoveredUrlStateAsync(existing, "rejected", reason, cancellationToken);
        await discoveryJobProgressService.RecordProcessedPageAsync(existing.JobId, existing.CategoryKey, "rejected", cancellationToken);
    }

    private async Task<bool> HasCapacityAsync(CrawlSource source, string categoryKey, string? jobId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(jobId))
        {
            var discoveredCount = await discoveredUrlStore.CountByScopeAsync(source.Id, categoryKey, jobId, cancellationToken);
            return discoveredCount < source.DiscoveryProfile.MaxUrlsPerRun;
        }

        var activeCount = await discoveryQueueStore.CountActiveAsync(source.Id, categoryKey, cancellationToken);
        return activeCount < source.DiscoveryProfile.MaxUrlsPerRun;
    }

    private static bool ShouldRetry(DiscoveryQueueItem queueItem, SourceDiscoveryProfile profile)
    {
        return queueItem.AttemptCount <= Math.Max(0, profile.MaxRetryCount);
    }

    private static bool ShouldRequeueCompletedSeed(DiscoveryQueueItem queueItem, SourceDiscoveryProfile profile, DateTime utcNow)
    {
        if (!string.Equals(queueItem.State, "completed", StringComparison.OrdinalIgnoreCase)
            || queueItem.Depth != 0
            || !string.IsNullOrWhiteSpace(queueItem.ParentUrl))
        {
            return false;
        }

        var anchorUtc = queueItem.CompletedUtc ?? queueItem.LastAttemptUtc ?? queueItem.EnqueuedUtc;
        return anchorUtc.Add(GetSeedReseedInterval(profile)) <= utcNow;
    }

    private static TimeSpan GetSeedReseedInterval(SourceDiscoveryProfile profile)
    {
        if (profile.SeedReseedIntervalHours > 0)
        {
            return TimeSpan.FromHours(profile.SeedReseedIntervalHours);
        }

        return DefaultSeedReseedInterval;
    }

    private static DateTime ComputeNextAttemptUtc(DiscoveryQueueItem queueItem, SourceDiscoveryProfile profile)
    {
        var exponent = Math.Max(0, queueItem.AttemptCount - 1);
        var baseDelay = Math.Max(1, profile.RetryBackoffBaseMs);
        var maxDelay = Math.Max(baseDelay, profile.RetryBackoffMaxMs);
        var computedDelay = Math.Min(maxDelay, (int)Math.Round(baseDelay * Math.Pow(2d, exponent), MidpointRounding.AwayFromZero));
        return DateTime.UtcNow.AddMilliseconds(computedDelay);
    }

    private static string MapDiscoveredState(DiscoveryQueueItem queueItem)
    {
        return queueItem.State switch
        {
            "queued" or "processing" => "pending",
            "completed" => "processed",
            "failed" => "rejected",
            "skipped" when queueItem.LastError is not null
                && (queueItem.LastError.Contains("robots", StringComparison.OrdinalIgnoreCase)
                    || queueItem.LastError.Contains("blocked", StringComparison.OrdinalIgnoreCase)) => "blocked",
            "skipped" => "skipped",
            _ => "pending"
        };
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
        bool countForJob,
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

        if (countForJob)
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

    private async Task UpdateDiscoveredUrlRetryStateAsync(DiscoveryQueueItem queueItem, string lastError, CancellationToken cancellationToken)
    {
        var existing = await discoveredUrlStore.GetByNormalizedUrlAsync(queueItem.SourceId, queueItem.CategoryKey, queueItem.NormalizedUrl, cancellationToken);
        if (existing is null)
        {
            return;
        }

        existing.State = "pending";
        existing.AttemptCount = Math.Max(existing.AttemptCount, queueItem.AttemptCount);
        existing.LastProcessedUtc = DateTime.UtcNow;
        existing.NextAttemptUtc = queueItem.NextAttemptUtc;
        existing.LastError = lastError;
        await discoveredUrlStore.UpsertAsync(existing, cancellationToken);
    }
}