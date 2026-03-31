namespace ProductNormaliser.Application.Sources;

public sealed class SourceCandidateSearchResponse
{
    public int ProviderResultCount { get; init; }
    public int EligibleResultCount { get; init; }
    public int DiscountedResultCount { get; init; }
    public int MergedDuplicateCount { get; init; }
    public IReadOnlyList<SourceCandidateSearchResult> Candidates { get; init; } = [];
    public IReadOnlyList<SourceCandidateDiscoveryDiagnostic> Diagnostics { get; init; } = [];
}