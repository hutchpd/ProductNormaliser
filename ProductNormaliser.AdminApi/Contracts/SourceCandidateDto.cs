namespace ProductNormaliser.AdminApi.Contracts;

public sealed class SourceCandidateDto
{
    public string CandidateKey { get; init; } = default!;
    public string DisplayName { get; init; } = default!;
    public string BaseUrl { get; init; } = default!;
    public string Host { get; init; } = default!;
    public string CandidateType { get; init; } = default!;
    public decimal ConfidenceScore { get; init; }
    public IReadOnlyList<string> MatchedCategoryKeys { get; init; } = [];
    public IReadOnlyList<string> MatchedBrandHints { get; init; } = [];
    public bool AlreadyRegistered { get; init; }
    public IReadOnlyList<string> DuplicateSourceIds { get; init; } = [];
    public IReadOnlyList<string> DuplicateSourceDisplayNames { get; init; } = [];
    public bool AllowedByGovernance { get; init; }
    public string? GovernanceWarning { get; init; }
    public SourceCandidateProbeDto Probe { get; init; } = new();
    public IReadOnlyList<SourceCandidateReasonDto> Reasons { get; init; } = [];
}