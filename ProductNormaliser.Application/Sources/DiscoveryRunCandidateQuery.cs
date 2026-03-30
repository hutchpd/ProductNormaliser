using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

public sealed class DiscoveryRunCandidateQuery
{
    public string? StateFilter { get; init; }
    public string? Sort { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 12;
}

public sealed class DiscoveryRunCandidatePage
{
    public IReadOnlyList<DiscoveryRunCandidate> Items { get; init; } = [];
    public string StateFilter { get; init; } = DiscoveryRunCandidateStateFilters.All;
    public string Sort { get; init; } = DiscoveryRunCandidateSortModes.ReviewPriority;
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long TotalCount { get; init; }
    public int TotalPages => PageSize <= 0 || TotalCount == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public DiscoveryRunCandidateRunSummary Summary { get; init; } = new();
}

public sealed class DiscoveryRunCandidateRunSummary
{
    public int RunCandidateCount { get; init; }
    public int ActiveCandidateCount { get; init; }
    public int ArchivedCandidateCount { get; init; }
    public int ProbeTimeoutCandidateCount { get; init; }
    public int RepresentativePageFetchFailureCandidateCount { get; init; }
    public int RepresentativeCategoryFetchFailureCount { get; init; }
    public int RepresentativeProductFetchFailureCount { get; init; }
    public int LlmTimeoutCandidateCount { get; init; }
}

public static class DiscoveryRunCandidateStateFilters
{
    public const string All = "all";
    public const string Active = "active";
    public const string Archived = "archived";
}

public static class DiscoveryRunCandidateSortModes
{
    public const string ReviewPriority = "review_priority";
    public const string ConfidenceDesc = "confidence_desc";
    public const string DuplicateRiskAsc = "duplicate_risk_asc";
    public const string UpdatedDesc = "updated_desc";
}