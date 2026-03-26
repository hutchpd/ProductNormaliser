namespace ProductNormaliser.Application.Sources;

public sealed class SourceCandidateSearchResponse
{
    public IReadOnlyList<SourceCandidateSearchResult> Candidates { get; init; } = [];
    public IReadOnlyList<SourceCandidateDiscoveryDiagnostic> Diagnostics { get; init; } = [];
}