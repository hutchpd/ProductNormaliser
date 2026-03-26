namespace ProductNormaliser.AdminApi.Contracts;

public sealed class DiscoveryRunCandidateDto
{
    public string CandidateKey { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string? PreviousState { get; init; }
    public string? AcceptedSourceId { get; init; }
    public string? StateMessage { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string Host { get; init; } = string.Empty;
    public string CandidateType { get; init; } = string.Empty;
    public IReadOnlyList<string> AllowedMarkets { get; init; } = [];
    public string? PreferredLocale { get; init; }
    public string MarketEvidence { get; init; } = string.Empty;
    public string LocaleEvidence { get; init; } = string.Empty;
    public decimal ConfidenceScore { get; init; }
    public decimal CrawlabilityScore { get; init; }
    public decimal ExtractabilityScore { get; init; }
    public decimal DuplicateRiskScore { get; init; }
    public string RecommendationStatus { get; init; } = string.Empty;
    public string RuntimeExtractionStatus { get; init; } = string.Empty;
    public string RuntimeExtractionMessage { get; init; } = string.Empty;
    public IReadOnlyList<string> MatchedCategoryKeys { get; init; } = [];
    public IReadOnlyList<string> MatchedBrandHints { get; init; } = [];
    public bool AlreadyRegistered { get; init; }
    public IReadOnlyList<string> DuplicateSourceIds { get; init; } = [];
    public IReadOnlyList<string> DuplicateSourceDisplayNames { get; init; } = [];
    public bool AllowedByGovernance { get; init; }
    public string? GovernanceWarning { get; init; }
    public SourceCandidateProbeDto Probe { get; init; } = new();
    public SourceCandidateAutomationAssessmentDto AutomationAssessment { get; init; } = new();
    public IReadOnlyList<SourceCandidateReasonDto> Reasons { get; init; } = [];
}