namespace ProductNormaliser.Core.Models;

public sealed class DiscoveryRunCandidate
{
    public string Id { get; set; } = default!;
    public string RunId { get; set; } = default!;
    public string CandidateKey { get; set; } = default!;
    public int Revision { get; set; } = 1;
    public string State { get; set; } = DiscoveryRunCandidateStates.Pending;
    public string? PreviousState { get; set; }
    public string? SupersededByCandidateKey { get; set; }
    public string? SuppressionDispositionId { get; set; }
    public string? AcceptedSourceId { get; set; }
    public string? StateMessage { get; set; }
    public string? ArchiveReason { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public string CandidateType { get; set; } = string.Empty;
    public IReadOnlyList<string> AllowedMarkets { get; set; } = [];
    public string? PreferredLocale { get; set; }
    public string MarketEvidence { get; set; } = "missing";
    public string LocaleEvidence { get; set; } = "missing";
    public decimal ConfidenceScore { get; set; }
    public decimal CrawlabilityScore { get; set; }
    public decimal ExtractabilityScore { get; set; }
    public decimal DuplicateRiskScore { get; set; }
    public string RecommendationStatus { get; set; } = string.Empty;
    public string RuntimeExtractionStatus { get; set; } = string.Empty;
    public string RuntimeExtractionMessage { get; set; } = string.Empty;
    public IReadOnlyList<string> MatchedCategoryKeys { get; set; } = [];
    public IReadOnlyList<string> MatchedBrandHints { get; set; } = [];
    public bool AlreadyRegistered { get; set; }
    public IReadOnlyList<string> DuplicateSourceIds { get; set; } = [];
    public IReadOnlyList<string> DuplicateSourceDisplayNames { get; set; } = [];
    public bool AllowedByGovernance { get; set; }
    public string? GovernanceWarning { get; set; }
    public DiscoveryRunCandidateProbe Probe { get; set; } = new();
    public DiscoveryRunCandidateAutomationAssessment AutomationAssessment { get; set; } = new();
    public List<DiscoveryRunCandidateReason> Reasons { get; set; } = [];
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? DecisionUtc { get; set; }
    public DateTime? ArchivedUtc { get; set; }
}