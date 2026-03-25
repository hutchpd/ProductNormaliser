using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProductNormaliser.Application.Crawls;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Discovery;

public sealed class ProductTargetEnqueuer(
    IProductTargetQueueStore productTargetQueueStore,
    ICrawlJobQueueWriter crawlJobQueueWriter,
    DiscoveryJobProgressService discoveryJobProgressService,
    ILogger<ProductTargetEnqueuer>? logger = null)
{
    private readonly ILogger<ProductTargetEnqueuer> logger = logger ?? NullLogger<ProductTargetEnqueuer>.Instance;

    public async Task<bool> EnqueueAsync(
        string? discoveryJobId,
        CrawlSource source,
        string categoryKey,
        string url,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        var queueId = BuildCrawlQueueId(source.Id, categoryKey, url);
        if (await productTargetQueueStore.GetByIdAsync(queueId, cancellationToken) is not null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        await crawlJobQueueWriter.UpsertAsync(new CrawlQueueItem
        {
            Id = queueId,
            JobId = null,
            InitiatingJobId = discoveryJobId,
            SourceName = source.Id,
            SourceUrl = url,
            CategoryKey = categoryKey.Trim(),
            Status = "queued",
            AttemptCount = 0,
            ConsecutiveFailureCount = 0,
            ImportanceScore = 0.75m,
            EnqueuedUtc = now,
            NextAttemptUtc = now
        }, cancellationToken);

        await discoveryJobProgressService.RecordConfirmedProductAsync(discoveryJobId, categoryKey, cancellationToken);

        logger.LogInformation(
            "Enqueued discovered product target {Url} for source {SourceId} in category {CategoryKey}",
            url,
            source.Id,
            categoryKey);

        return true;
    }

    private static string BuildCrawlQueueId(string sourceId, string categoryKey, string url)
    {
        return $"crawl:discovered:{sourceId}:{categoryKey}:{Hash(NormalizeUrl(url))}";
    }

    private static string NormalizeUrl(string url)
    {
        var uri = new Uri(url.Trim(), UriKind.Absolute);
        var builder = new UriBuilder(uri)
        {
            Fragment = string.Empty,
            Query = string.Empty
        };

        return builder.Uri.ToString().TrimEnd('/');
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}