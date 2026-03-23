using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Discovery;

public interface IDiscoveryQueueService
{
    Task<DiscoveryQueueLease?> DequeueAsync(CancellationToken cancellationToken);
    Task<bool> EnqueueAsync(CrawlSource source, string categoryKey, string url, string itemType, int depth, string? parentUrl, string? jobId, CancellationToken cancellationToken);
    Task<bool> EnqueueProductAsync(CrawlSource source, string categoryKey, string url, int depth, string? parentUrl, string? jobId, CancellationToken cancellationToken);
    Task MarkCompletedAsync(string queueItemId, CancellationToken cancellationToken);
    Task MarkSkippedAsync(string queueItemId, string reason, CancellationToken cancellationToken);
    Task MarkFailedAsync(string queueItemId, string reason, CancellationToken cancellationToken);
}