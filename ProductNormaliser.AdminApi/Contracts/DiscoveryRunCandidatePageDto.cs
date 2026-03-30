namespace ProductNormaliser.AdminApi.Contracts;

public sealed class DiscoveryRunCandidatePageDto
{
    public IReadOnlyList<DiscoveryRunCandidateDto> Items { get; init; } = [];
    public string StateFilter { get; init; } = string.Empty;
    public string Sort { get; init; } = string.Empty;
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long TotalCount { get; init; }
    public int TotalPages { get; init; }
    public DiscoveryRunCandidateRunSummaryDto Summary { get; init; } = new();
}

public sealed class DiscoveryRunCandidateRunSummaryDto
{
    public int RunCandidateCount { get; init; }
    public int ActiveCandidateCount { get; init; }
    public int ArchivedCandidateCount { get; init; }
    public int LlmMeasuredCandidateCount { get; init; }
    public int LlmBudgetProbeCappedCandidateCount { get; init; }
    public int ProbeTimeoutCandidateCount { get; init; }
    public int RepresentativePageFetchFailureCandidateCount { get; init; }
    public int RepresentativeCategoryFetchFailureCount { get; init; }
    public int RepresentativeProductFetchFailureCount { get; init; }
    public int LlmTimeoutCandidateCount { get; init; }
    public long? AverageLlmBudgetMs { get; init; }
    public decimal? AverageLlmBudgetUtilizationPercent { get; init; }
    public IReadOnlyList<DiscoveryRunCandidateBlockerSummaryDto> AutoAcceptBlockers { get; init; } = [];
}

public sealed class DiscoveryRunCandidateBlockerSummaryDto
{
    public string Code { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
}