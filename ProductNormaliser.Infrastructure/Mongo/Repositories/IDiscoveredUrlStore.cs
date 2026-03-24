using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public interface IDiscoveredUrlStore
{
    Task<DiscoveredUrl?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<DiscoveredUrl?> GetByNormalizedUrlAsync(string sourceId, string categoryKey, string normalizedUrl, CancellationToken cancellationToken = default);
    Task<long> CountByScopeAsync(string sourceId, string categoryKey, string? jobId, CancellationToken cancellationToken = default);
    Task UpsertAsync(DiscoveredUrl item, CancellationToken cancellationToken = default);
}