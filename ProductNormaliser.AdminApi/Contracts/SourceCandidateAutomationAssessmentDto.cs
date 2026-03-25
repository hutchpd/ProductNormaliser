namespace ProductNormaliser.AdminApi.Contracts;

public sealed class SourceCandidateAutomationAssessmentDto
{
    public string RequestedMode { get; init; } = string.Empty;
    public string Decision { get; init; } = string.Empty;
    public bool MarketMatchApproved { get; init; }
    public bool MarketEvidenceStrongEnough { get; init; }
    public bool GovernancePassed { get; init; }
    public bool DuplicateRiskAccepted { get; init; }
    public bool RepresentativeValidationPassed { get; init; }
    public bool ExtractabilityConfidencePassed { get; init; }
    public bool YieldConfidencePassed { get; init; }
    public bool EligibleForSuggestion { get; init; }
    public bool EligibleForAutoAccept { get; init; }
    public bool EligibleForAutoSeed { get; init; }
    public string MarketEvidence { get; init; } = string.Empty;
    public string LocaleEvidence { get; init; } = string.Empty;
    public IReadOnlyList<string> SupportingReasons { get; init; } = [];
    public IReadOnlyList<string> BlockingReasons { get; init; } = [];
}