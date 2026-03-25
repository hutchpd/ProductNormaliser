namespace ProductNormaliser.Application.Sources;

public sealed class SourceCandidateAutomationAssessment
{
    public const string DecisionManualOnly = "manual_only";
    public const string DecisionSuggestAccept = "suggest_accept";
    public const string DecisionAutoAcceptAndSeed = "auto_accept_and_seed";

    public string RequestedMode { get; init; } = string.Empty;
    public string Decision { get; init; } = DecisionManualOnly;
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
    public string MarketEvidence { get; init; } = "missing";
    public string LocaleEvidence { get; init; } = "missing";
    public IReadOnlyList<string> SupportingReasons { get; init; } = [];
    public IReadOnlyList<string> BlockingReasons { get; init; } = [];
}