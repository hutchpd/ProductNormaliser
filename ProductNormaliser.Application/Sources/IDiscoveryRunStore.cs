using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

public interface IDiscoveryRunStore
{
    Task<DiscoveryRunPage> ListAsync(DiscoveryRunQuery query, CancellationToken cancellationToken = default);

    Task<DiscoveryRun?> GetAsync(string runId, CancellationToken cancellationToken = default);

    Task<DiscoveryRun?> GetNextQueuedAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DiscoveryRun>> ListByStatusesAsync(IReadOnlyCollection<string> statuses, CancellationToken cancellationToken = default);

    Task UpsertAsync(DiscoveryRun run, CancellationToken cancellationToken = default);
}