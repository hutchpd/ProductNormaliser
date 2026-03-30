namespace ProductNormaliser.Core.Models;

public sealed class DiscoveryRunCandidateAutomationAssessment
{
    public string RequestedMode { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public bool MarketMatchApproved { get; set; }
    public bool MarketEvidenceStrongEnough { get; set; }
    public bool GovernancePassed { get; set; }
    public bool DuplicateRiskAccepted { get; set; }
    public bool RepresentativeValidationPassed { get; set; }
    public bool ExtractabilityConfidencePassed { get; set; }
    public bool YieldConfidencePassed { get; set; }
    public bool SuggestionBreadthPassed { get; set; }
    public bool AutoAcceptBreadthPassed { get; set; }
    public bool LocaleAligned { get; set; }
    public bool CrawlabilityPassed { get; set; }
    public bool CategoryRelevancePassed { get; set; }
    public bool CatalogLikelihoodPassed { get; set; }
    public bool SuggestionConfidencePassed { get; set; }
    public bool AutoAcceptConfidencePassed { get; set; }
    public bool EligibleForSuggestion { get; set; }
    public bool EligibleForAutoAccept { get; set; }
    public bool EligibleForAutoSeed { get; set; }
    public string MarketEvidence { get; set; } = string.Empty;
    public string LocaleEvidence { get; set; } = string.Empty;
    public IReadOnlyList<string> SupportingReasons { get; set; } = [];
    public IReadOnlyList<string> BlockingReasons { get; set; } = [];
}