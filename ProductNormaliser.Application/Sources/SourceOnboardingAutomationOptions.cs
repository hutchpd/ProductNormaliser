using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

public sealed class SourceOnboardingAutomationOptions
{
    public const string SectionName = "SourceOnboardingAutomation";

    public string DefaultMode { get; set; } = SourceAutomationModes.OperatorAssisted;
    public int AutomationCategorySampleBudget { get; set; } = 3;
    public int AutomationProductSampleBudget { get; set; } = 3;
    public decimal SuggestMinConfidenceScore { get; set; } = 78m;
    public decimal AutoAcceptMinConfidenceScore { get; set; } = 90m;
    public decimal MinCrawlabilityScore { get; set; } = 60m;
    public decimal MinCategoryRelevanceScore { get; set; } = 40m;
    public decimal MinExtractabilityScore { get; set; } = 65m;
    public decimal MinCatalogLikelihoodScore { get; set; } = 55m;
    public decimal MaxDuplicateRiskScore { get; set; } = 15m;
    public decimal MinYieldConfidenceScore { get; set; } = 70m;
    public int SuggestMinReachableCategorySamples { get; set; } = 2;
    public int SuggestMinReachableProductSamples { get; set; } = 2;
    public int SuggestMinRuntimeCompatibleProductSamples { get; set; } = 2;
    public int AutoAcceptMinReachableCategorySamples { get; set; } = 3;
    public int AutoAcceptMinReachableProductSamples { get; set; } = 3;
    public int AutoAcceptMinRuntimeCompatibleProductSamples { get; set; } = 3;
    public int AutoAcceptMinStructuredEvidenceProductSamples { get; set; } = 2;
    public int MaxAutoAcceptedCandidatesPerRun { get; set; } = 1;
}