using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Crawls;

public interface ICrawlJobQueueWriter
{
    Task UpsertAsync(CrawlQueueItem item, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CrawlQueueItem>> CancelQueuedItemsAsync(string jobId, string reason, CancellationToken cancellationToken = default);
}