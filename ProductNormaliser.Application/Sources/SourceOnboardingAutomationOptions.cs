using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

public sealed class SourceOnboardingAutomationOptions
{
    public const string SectionName = "SourceOnboardingAutomation";

    public string DefaultMode { get; set; } = SourceAutomationModes.OperatorAssisted;
    public decimal SuggestMinConfidenceScore { get; set; } = 78m;
    public decimal AutoAcceptMinConfidenceScore { get; set; } = 90m;
    public decimal MinCrawlabilityScore { get; set; } = 60m;
    public decimal MinCategoryRelevanceScore { get; set; } = 40m;
    public decimal MinExtractabilityScore { get; set; } = 65m;
    public decimal MinCatalogLikelihoodScore { get; set; } = 55m;
    public decimal MaxDuplicateRiskScore { get; set; } = 15m;
    public decimal MinYieldConfidenceScore { get; set; } = 70m;
    public int MaxAutoAcceptedCandidatesPerRun { get; set; } = 1;
}