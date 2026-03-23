using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Discovery;

public interface IProductTargetQueueStore
{
    Task<CrawlQueueItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
}