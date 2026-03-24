using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public interface IDiscoveryQueueStore
{
    Task<DiscoveryQueueItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task UpsertAsync(DiscoveryQueueItem item, CancellationToken cancellationToken = default);
    Task<DiscoveryQueueItem?> TryAcquireAsync(string id, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<long> CountActiveAsync(string sourceId, string categoryKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DiscoveryQueueItem>> ListQueuedAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}