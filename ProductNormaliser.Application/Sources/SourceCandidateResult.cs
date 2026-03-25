namespace ProductNormaliser.Application.Sources;

public sealed class SourceCandidateResult
{
    public const string RecommendationRecommended = "recommended";
    public const string RecommendationManualReview = "manual_review";
    public const string RecommendationDoNotAccept = "do_not_accept";

    public string CandidateKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string Host { get; init; } = string.Empty;
    public string CandidateType { get; init; } = string.Empty;
    public IReadOnlyList<string> AllowedMarkets { get; init; } = [];
    public string? PreferredLocale { get; init; }
    public string MarketEvidence { get; init; } = "missing";
    public string LocaleEvidence { get; init; } = "missing";
    public decimal ConfidenceScore { get; init; }
    public decimal CrawlabilityScore { get; init; }
    public decimal ExtractabilityScore { get; init; }
    public decimal DuplicateRiskScore { get; init; }
    public string RecommendationStatus { get; init; } = RecommendationManualReview;
    public IReadOnlyList<string> MatchedCategoryKeys { get; init; } = [];
    public IReadOnlyList<string> MatchedBrandHints { get; init; } = [];
    public bool AlreadyRegistered { get; init; }
    public IReadOnlyList<string> DuplicateSourceIds { get; init; } = [];
    public IReadOnlyList<string> DuplicateSourceDisplayNames { get; init; } = [];
    public bool AllowedByGovernance { get; init; }
    public string? GovernanceWarning { get; init; }
    public SourceCandidateProbeResult Probe { get; init; } = new();
    public SourceCandidateAutomationAssessment AutomationAssessment { get; init; } = new();
    public IReadOnlyList<SourceCandidateReason> Reasons { get; init; } = [];
}