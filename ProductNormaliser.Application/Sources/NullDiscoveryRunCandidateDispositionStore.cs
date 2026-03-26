using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

internal sealed class NullDiscoveryRunCandidateDispositionStore : IDiscoveryRunCandidateDispositionStore
{
    public static NullDiscoveryRunCandidateDispositionStore Instance { get; } = new();

    public Task<IReadOnlyList<DiscoveryRunCandidateDisposition>> FindActiveMatchesAsync(
        string scopeFingerprint,
        string normalizedHost,
        string normalizedBaseUrl,
        string normalizedDisplayName,
        IReadOnlyCollection<string> allowedMarkets,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<DiscoveryRunCandidateDisposition>>([]);
    }

    public Task UpsertAsync(DiscoveryRunCandidateDisposition disposition, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}