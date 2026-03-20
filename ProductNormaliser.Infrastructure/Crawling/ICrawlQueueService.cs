using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Crawling;

public interface ICrawlQueueService
{
    Task<CrawlQueueLease?> DequeueAsync(CancellationToken cancellationToken);
    Task MarkCompletedAsync(string queueItemId, CancellationToken cancellationToken);
    Task MarkSkippedAsync(string queueItemId, string reason, CancellationToken cancellationToken);
    Task MarkFailedAsync(string queueItemId, string reason, CancellationToken cancellationToken);
}