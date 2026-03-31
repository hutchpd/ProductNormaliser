namespace ProductNormaliser.Application.Sources;

public interface IProgressReportingSourceCandidateSearchProvider : ISourceCandidateSearchProvider
{
    Task<SourceCandidateSearchResponse> SearchAsync(
        DiscoverSourceCandidatesRequest request,
        Func<SourceCandidateDiscoveryDiagnostic, CancellationToken, Task> progressReporter,
        CancellationToken cancellationToken = default);
}