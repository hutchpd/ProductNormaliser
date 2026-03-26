using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

public interface IDiscoveryRunCandidateDispositionStore
{
    Task<IReadOnlyList<DiscoveryRunCandidateDisposition>> FindActiveMatchesAsync(
        string scopeFingerprint,
        string normalizedHost,
        string normalizedBaseUrl,
        string normalizedDisplayName,
        IReadOnlyCollection<string> allowedMarkets,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(DiscoveryRunCandidateDisposition disposition, CancellationToken cancellationToken = default);
}