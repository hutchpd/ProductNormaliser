using MongoDB.Driver;
using Microsoft.Extensions.Options;
using ProductNormaliser.Application.Crawls;
using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Mongo;
using ProductNormaliser.Infrastructure.Mongo.Repositories;

namespace ProductNormaliser.Infrastructure.Crawling;

public sealed class CrawlQueueService(
    ICrawlQueueStore crawlQueueStore,
    ICrawlPriorityService crawlPriorityService,
    ICrawlBackoffService crawlBackoffService,
    ICrawlJobService crawlJobService,
    MongoDbContext mongoDbContext,
    IOptions<CrawlPipelineOptions> options) : ICrawlQueueService
{
    private readonly CrawlPipelineOptions crawlOptions = options.Value;

    public async Task<CrawlQueueLease?> DequeueAsync(CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        CrawlQueueItem? queueItem = null;

        foreach (var assessment in await crawlPriorityService.GetPrioritiesAsync(utcNow, cancellationToken))
        {
            queueItem = await crawlQueueStore.TryAcquireAsync(assessment.QueueItem.Id, utcNow, cancellationToken);
            if (queueItem is not null)
            {
                break;
            }
        }

        if (queueItem is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(queueItem.JobId))
        {
            await crawlJobService.MarkStartedAsync(queueItem.JobId, cancellationToken);
        }

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
        if (queueItem is null || !string.Equals(queueItem.Status, "processing", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(queueItem.JobId))
        {
            queueItem.Status = "completed";
            queueItem.ConsecutiveFailureCount = 0;
            queueItem.LastError = null;
            queueItem.NextAttemptUtc = null;
            await crawlQueueStore.UpsertAsync(queueItem, cancellationToken);
            await crawlJobService.RecordTargetOutcomeAsync(queueItem.JobId, queueItem.CategoryKey, "completed", cancellationToken);
            return;
        }

        queueItem.Status = "queued";
        queueItem.ConsecutiveFailureCount = 0;
        queueItem.LastError = null;
        queueItem.NextAttemptUtc = await ComputeNextAttemptAsync(queueItem, cancellationToken);
        await crawlQueueStore.UpsertAsync(queueItem, cancellationToken);
    }

    public async Task MarkSkippedAsync(string queueItemId, string reason, CancellationToken cancellationToken)
    {
        var queueItem = await crawlQueueStore.GetByIdAsync(queueItemId, cancellationToken);
        if (queueItem is null || !string.Equals(queueItem.Status, "processing", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(queueItem.JobId))
        {
            queueItem.Status = "skipped";
            queueItem.ConsecutiveFailureCount = 0;
            queueItem.LastError = reason;
            queueItem.NextAttemptUtc = null;
            await crawlQueueStore.UpsertAsync(queueItem, cancellationToken);
            await crawlJobService.RecordTargetOutcomeAsync(queueItem.JobId, queueItem.CategoryKey, "skipped", cancellationToken);
            return;
        }

        queueItem.Status = "queued";
        queueItem.ConsecutiveFailureCount = 0;
        queueItem.LastError = reason;
        queueItem.NextAttemptUtc = await ComputeNextAttemptAsync(queueItem, cancellationToken);
        await crawlQueueStore.UpsertAsync(queueItem, cancellationToken);
    }

    public async Task MarkFailedAsync(string queueItemId, string reason, CancellationToken cancellationToken)
    {
        var queueItem = await crawlQueueStore.GetByIdAsync(queueItemId, cancellationToken);
        if (queueItem is null || !string.Equals(queueItem.Status, "processing", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(queueItem.JobId))
        {
            queueItem.ConsecutiveFailureCount += 1;

            if (await ShouldRetryJobItemAsync(queueItem, cancellationToken))
            {
                queueItem.Status = "queued";
                queueItem.LastError = reason;
                queueItem.NextAttemptUtc = await ComputeNextAttemptAsync(queueItem, cancellationToken);
                await crawlQueueStore.UpsertAsync(queueItem, cancellationToken);
                return;
            }

            queueItem.Status = "failed";
            queueItem.LastError = reason;
            queueItem.NextAttemptUtc = null;
            await crawlQueueStore.UpsertAsync(queueItem, cancellationToken);
            await crawlJobService.RecordTargetOutcomeAsync(queueItem.JobId, queueItem.CategoryKey, "failed", cancellationToken);
            return;
        }

        queueItem.Status = "queued";
        queueItem.ConsecutiveFailureCount += 1;
        queueItem.LastError = reason;
        queueItem.NextAttemptUtc = await ComputeNextAttemptAsync(queueItem, cancellationToken);
        await crawlQueueStore.UpsertAsync(queueItem, cancellationToken);
    }

    private async Task<bool> ShouldRetryJobItemAsync(CrawlQueueItem queueItem, CancellationToken cancellationToken)
    {
        if (queueItem.AttemptCount > Math.Max(0, crawlOptions.TransientRetryCount))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(queueItem.JobId))
        {
            return false;
        }

        var job = await crawlJobService.GetAsync(queueItem.JobId, cancellationToken);
        return job is not null
            && !string.Equals(job.Status, CrawlJobStatuses.CancelRequested, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(job.Status, CrawlJobStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(job.Status, CrawlJobStatuses.Completed, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(job.Status, CrawlJobStatuses.CompletedWithFailures, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(job.Status, CrawlJobStatuses.Failed, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<DateTime> ComputeNextAttemptAsync(CrawlQueueItem queueItem, CancellationToken cancellationToken)
    {
        var latestSnapshot = await mongoDbContext.SourceQualitySnapshots
            .Find(snapshot => snapshot.SourceName == queueItem.SourceName && snapshot.CategoryKey == queueItem.CategoryKey)
            .SortByDescending(snapshot => snapshot.TimestampUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var crawlLogs = await mongoDbContext.CrawlLogs
            .Find(log => log.SourceName == queueItem.SourceName && log.Url == queueItem.SourceUrl)
            .SortByDescending(log => log.TimestampUtc)
            .Limit(20)
            .ToListAsync(cancellationToken);

        var volatility = new PageVolatilityProfile
        {
            ChangeFrequencyScore = CalculateChangeFrequency(crawlLogs),
            PriceVolatilityScore = latestSnapshot?.PriceVolatilityScore ?? 0m,
            SpecStabilityScore = latestSnapshot?.SpecStabilityScore ?? 1m,
            FailureRate = CalculateFailureRate(crawlLogs)
        };
        volatility.PageVolatilityScore = decimal.Round(
            volatility.ChangeFrequencyScore * 0.40m
            + volatility.PriceVolatilityScore * 0.35m
            + (1m - volatility.SpecStabilityScore) * 0.25m,
            4,
            MidpointRounding.AwayFromZero);

        return crawlBackoffService.ComputeNextAttempt(
            new CrawlContext
            {
                SourceName = queueItem.SourceName,
                CategoryKey = queueItem.CategoryKey,
                SourceUrl = queueItem.SourceUrl,
                ImportanceScore = queueItem.ImportanceScore,
                ConsecutiveFailureCount = queueItem.ConsecutiveFailureCount,
                LastAttemptUtc = queueItem.LastAttemptUtc,
                UtcNow = DateTime.UtcNow
            },
            latestSnapshot,
            volatility);
    }

    private static decimal CalculateChangeFrequency(IReadOnlyCollection<CrawlLog> crawlLogs)
    {
        if (crawlLogs.Count == 0)
        {
            return 0.50m;
        }

        return decimal.Round((decimal)crawlLogs.Count(log => log.HadMeaningfulChange) / crawlLogs.Count, 4, MidpointRounding.AwayFromZero);
    }

    private static decimal CalculateFailureRate(IReadOnlyCollection<CrawlLog> crawlLogs)
    {
        if (crawlLogs.Count == 0)
        {
            return 0m;
        }

        return decimal.Round((decimal)crawlLogs.Count(log => string.Equals(log.Status, "failed", StringComparison.OrdinalIgnoreCase)) / crawlLogs.Count, 4, MidpointRounding.AwayFromZero);
    }
}