using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProductNormaliser.Application.Crawls;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Discovery;

public sealed class DiscoveryJobProgressService(
    ICrawlJobStore crawlJobStore,
    ILogger<DiscoveryJobProgressService>? logger = null)
{
    private readonly ILogger<DiscoveryJobProgressService> logger = logger ?? NullLogger<DiscoveryJobProgressService>.Instance;

    public Task RecordDiscoveredUrlAsync(string? jobId, string categoryKey, CancellationToken cancellationToken = default)
    {
        return UpdateAsync(jobId, categoryKey, (job, breakdown) =>
        {
            job.TotalTargets += 1;
            job.DiscoveredUrlCount += 1;
            breakdown.TotalTargets += 1;
            breakdown.DiscoveredUrlCount += 1;
        }, cancellationToken);
    }

    public Task RecordConfirmedProductAsync(string? jobId, string categoryKey, CancellationToken cancellationToken = default)
    {
        return UpdateAsync(jobId, categoryKey, (job, breakdown) =>
        {
            job.ConfirmedProductCount += 1;
            breakdown.ConfirmedProductCount += 1;
        }, cancellationToken);
    }

    public Task RecordProcessedPageAsync(string? jobId, string categoryKey, string outcome, CancellationToken cancellationToken = default)
    {
        return UpdateAsync(jobId, categoryKey, (job, breakdown) =>
        {
            var normalizedOutcome = NormalizeRequiredValue(outcome, nameof(outcome)).ToLowerInvariant();
            job.ProcessedTargets += 1;
            breakdown.ProcessedTargets += 1;

            switch (normalizedOutcome)
            {
                case "completed":
                    job.SuccessCount += 1;
                    breakdown.SuccessCount += 1;
                    break;
                case "blocked":
                    job.SkippedCount += 1;
                    job.BlockedPageCount += 1;
                    breakdown.SkippedCount += 1;
                    breakdown.BlockedPageCount += 1;
                    break;
                case "rejected":
                    job.FailedCount += 1;
                    job.RejectedPageCount += 1;
                    breakdown.FailedCount += 1;
                    breakdown.RejectedPageCount += 1;
                    break;
                default:
                    job.SkippedCount += 1;
                    breakdown.SkippedCount += 1;
                    break;
            }

            if (job.ProcessedTargets >= job.TotalTargets && job.TotalTargets > 0)
            {
                job.EstimatedCompletion = DateTime.UtcNow;
                job.Status = DetermineCompletedStatus(job);
            }
        }, cancellationToken);
    }

    private async Task UpdateAsync(string? jobId, string categoryKey, Action<CrawlJob, CrawlJobCategoryBreakdown> update, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return;
        }

        var job = await crawlJobStore.GetAsync(NormalizeRequiredValue(jobId, nameof(jobId)), cancellationToken);
        if (job is null)
        {
            return;
        }

        var normalizedCategoryKey = NormalizeRequiredValue(categoryKey, nameof(categoryKey));
        var breakdown = job.PerCategoryBreakdown.FirstOrDefault(item => string.Equals(item.CategoryKey, normalizedCategoryKey, StringComparison.OrdinalIgnoreCase));
        if (breakdown is null)
        {
            breakdown = new CrawlJobCategoryBreakdown { CategoryKey = normalizedCategoryKey };
            job.PerCategoryBreakdown.Add(breakdown);
        }

        update(job, breakdown);
        if (string.Equals(job.Status, CrawlJobStatuses.Pending, StringComparison.OrdinalIgnoreCase))
        {
            job.Status = CrawlJobStatuses.Running;
        }

        job.LastUpdatedAt = DateTime.UtcNow;
        await crawlJobStore.UpsertAsync(job, cancellationToken);

        logger.LogDebug(
            "Updated discovery job {JobId} progress for category {CategoryKey}; discovered={DiscoveredUrlCount}, confirmed={ConfirmedProductCount}, rejected={RejectedPageCount}, blocked={BlockedPageCount}",
            job.JobId,
            normalizedCategoryKey,
            job.DiscoveredUrlCount,
            job.ConfirmedProductCount,
            job.RejectedPageCount,
            job.BlockedPageCount);
    }

    private static string NormalizeRequiredValue(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim();
    }

    private static string DetermineCompletedStatus(CrawlJob job)
    {
        if (string.Equals(job.Status, CrawlJobStatuses.CancelRequested, StringComparison.OrdinalIgnoreCase)
            || job.CancelledCount > 0)
        {
            return CrawlJobStatuses.Cancelled;
        }

        return job.FailedCount switch
        {
            0 => CrawlJobStatuses.Completed,
            _ when job.FailedCount >= job.TotalTargets => CrawlJobStatuses.Failed,
            _ => CrawlJobStatuses.CompletedWithFailures
        };
    }
}