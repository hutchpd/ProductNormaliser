namespace ProductNormaliser.Application.Sources;

public interface ISourceCandidateSearchProvider
{
    Task<SourceCandidateSearchResponse> SearchAsync(DiscoverSourceCandidatesRequest request, CancellationToken cancellationToken = default);
}