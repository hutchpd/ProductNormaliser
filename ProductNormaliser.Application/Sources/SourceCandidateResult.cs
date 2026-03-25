namespace ProductNormaliser.Application.Sources;

public sealed class SourceCandidateResult
{
    public string CandidateKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string Host { get; init; } = string.Empty;
    public string CandidateType { get; init; } = string.Empty;
    public decimal ConfidenceScore { get; init; }
    public IReadOnlyList<string> MatchedCategoryKeys { get; init; } = [];
    public IReadOnlyList<string> MatchedBrandHints { get; init; } = [];
    public bool AlreadyRegistered { get; init; }
    public IReadOnlyList<string> DuplicateSourceIds { get; init; } = [];
    public IReadOnlyList<string> DuplicateSourceDisplayNames { get; init; } = [];
    public bool AllowedByGovernance { get; init; }
    public string? GovernanceWarning { get; init; }
    public SourceCandidateProbeResult Probe { get; init; } = new();
    public IReadOnlyList<SourceCandidateReason> Reasons { get; init; } = [];
}