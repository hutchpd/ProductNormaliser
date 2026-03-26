namespace ProductNormaliser.AdminApi.Contracts;

public sealed class SourceOnboardingAutomationSettingsDto
{
    public string DefaultMode { get; init; } = string.Empty;
    public int MaxAutoAcceptedCandidatesPerRun { get; init; }
    public int AutomationCategorySampleBudget { get; init; }
    public int AutomationProductSampleBudget { get; init; }
    public decimal SuggestMinConfidenceScore { get; init; }
    public decimal AutoAcceptMinConfidenceScore { get; init; }
    public decimal MinCrawlabilityScore { get; init; }
    public decimal MinCategoryRelevanceScore { get; init; }
    public decimal MinExtractabilityScore { get; init; }
    public decimal MinCatalogLikelihoodScore { get; init; }
    public decimal MaxDuplicateRiskScore { get; init; }
    public decimal MinYieldConfidenceScore { get; init; }
    public int SuggestMinReachableCategorySamples { get; init; }
    public int SuggestMinReachableProductSamples { get; init; }
    public int SuggestMinRuntimeCompatibleProductSamples { get; init; }
    public int AutoAcceptMinReachableCategorySamples { get; init; }
    public int AutoAcceptMinReachableProductSamples { get; init; }
    public int AutoAcceptMinRuntimeCompatibleProductSamples { get; init; }
    public int AutoAcceptMinStructuredEvidenceProductSamples { get; init; }
}