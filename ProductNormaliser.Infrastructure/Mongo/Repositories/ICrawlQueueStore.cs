using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public interface ICrawlQueueStore
{
    Task<CrawlQueueItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task UpsertAsync(CrawlQueueItem item, CancellationToken cancellationToken = default);
    Task<CrawlQueueItem?> GetNextQueuedAsync(DateTime utcNow, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CrawlQueueItem>> ListQueuedAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}