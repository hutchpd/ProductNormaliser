namespace ProductNormaliser.Application.Sources;

public sealed class NoOpSourceCandidateSearchProvider : ISourceCandidateSearchProvider
{
    public Task<SourceCandidateSearchResponse> SearchAsync(DiscoverSourceCandidatesRequest request, CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = cancellationToken;

        // TODO: integrate a real search provider for retailer/manufacturer candidate lookup.
        return Task.FromResult(new SourceCandidateSearchResponse
        {
            Diagnostics =
            [
                new SourceCandidateDiscoveryDiagnostic
                {
                    Code = "search_provider_not_configured",
                    Severity = SourceCandidateDiscoveryDiagnostic.SeverityWarning,
                    Title = "Search provider not configured",
                    Message = "Source candidate discovery is running without a configured external search provider. Manual source registration is still available."
                }
            ]
        });
    }
}