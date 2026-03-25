namespace ProductNormaliser.Application.Sources;

public interface ISourceCandidateDiscoveryService
{
    Task<SourceCandidateDiscoveryResult> DiscoverAsync(DiscoverSourceCandidatesRequest request, CancellationToken cancellationToken = default);
}