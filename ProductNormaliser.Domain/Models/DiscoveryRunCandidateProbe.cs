namespace ProductNormaliser.Core.Models;

public sealed class DiscoveryRunCandidateProbe
{
    public bool HomePageReachable { get; set; }
    public bool RobotsTxtReachable { get; set; }
    public bool ProbeTimedOut { get; set; }
    public bool ProbeFailed { get; set; }
    public bool SitemapDetected { get; set; }
    public IReadOnlyList<string> SitemapUrls { get; set; } = [];
    public decimal CrawlabilityScore { get; set; }
    public decimal CategoryRelevanceScore { get; set; }
    public decimal HeuristicExtractabilityScore { get; set; }
    public decimal ExtractabilityScore { get; set; }
    public decimal CatalogLikelihoodScore { get; set; }
    public string? RepresentativeCategoryPageUrl { get; set; }
    public bool RepresentativeCategoryPageReachable { get; set; }
    public bool RepresentativeCategoryPageFetchFailed { get; set; }
    public string? RepresentativeProductPageUrl { get; set; }
    public bool RepresentativeProductPageReachable { get; set; }
    public bool RepresentativeProductPageFetchFailed { get; set; }
    public bool RuntimeExtractionCompatible { get; set; }
    public int RepresentativeRuntimeProductCount { get; set; }
    public int AutomationCategorySampleCount { get; set; }
    public int AutomationReachableCategorySampleCount { get; set; }
    public int AutomationProductSampleCount { get; set; }
    public int AutomationReachableProductSampleCount { get; set; }
    public int AutomationRuntimeCompatibleProductSampleCount { get; set; }
    public int AutomationStructuredProductEvidenceSampleCount { get; set; }
    public int AutomationTechnicalAttributeEvidenceSampleCount { get; set; }
    public bool StructuredProductEvidenceDetected { get; set; }
    public bool TechnicalAttributeEvidenceDetected { get; set; }
    public bool LlmAcceptedRepresentativeProductPage { get; set; }
    public bool LlmRejectedRepresentativeProductPage { get; set; }
    public bool LlmDisagreedWithHeuristics { get; set; }
    public bool LlmDetectedSpecifications { get; set; }
    public string? LlmDetectedCategory { get; set; }
    public decimal? LlmConfidenceScore { get; set; }
    public bool LlmTimedOut { get; set; }
    public string? LlmReason { get; set; }
    public long? LlmBudgetMs { get; set; }
    public bool LlmBudgetLimitedByProbe { get; set; }
    public int ProbeAttemptCount { get; set; }
    public long ProbeElapsedMs { get; set; }
    public long? LlmElapsedMs { get; set; }
    public bool NonCatalogContentHeavy { get; set; }
    public IReadOnlyList<string> CategoryPageHints { get; set; } = [];
    public IReadOnlyList<string> LikelyListingUrlPatterns { get; set; } = [];
    public IReadOnlyList<string> LikelyProductUrlPatterns { get; set; } = [];
}