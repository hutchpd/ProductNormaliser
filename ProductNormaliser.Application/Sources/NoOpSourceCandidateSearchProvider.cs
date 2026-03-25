namespace ProductNormaliser.Application.Sources;

public sealed class NoOpSourceCandidateSearchProvider : ISourceCandidateSearchProvider
{
    public Task<IReadOnlyList<SourceCandidateSearchResult>> SearchAsync(DiscoverSourceCandidatesRequest request, CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = cancellationToken;

        // TODO: integrate a real search provider for retailer/manufacturer candidate lookup.
        return Task.FromResult<IReadOnlyList<SourceCandidateSearchResult>>([]);
    }
}