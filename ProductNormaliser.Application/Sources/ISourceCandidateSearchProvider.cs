namespace ProductNormaliser.Application.Sources;

public interface ISourceCandidateSearchProvider
{
    Task<IReadOnlyList<SourceCandidateSearchResult>> SearchAsync(DiscoverSourceCandidatesRequest request, CancellationToken cancellationToken = default);
}