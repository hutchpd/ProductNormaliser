using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

public interface IDiscoveryRunCandidateStore
{
    Task<IReadOnlyList<DiscoveryRunCandidate>> ListByRunAsync(string runId, CancellationToken cancellationToken = default);

    Task<DiscoveryRunCandidate?> GetAsync(string runId, string candidateKey, CancellationToken cancellationToken = default);

    Task UpsertAsync(DiscoveryRunCandidate candidate, CancellationToken cancellationToken = default);

    Task<bool> TryUpdateAsync(DiscoveryRunCandidate candidate, int expectedRevision, CancellationToken cancellationToken = default);
}