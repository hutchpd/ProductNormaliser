namespace ProductNormaliser.Application.Sources;

public sealed class SourceCandidateProbeResult
{
    public bool HomePageReachable { get; init; }
    public bool RobotsTxtReachable { get; init; }
    public bool SitemapDetected { get; init; }
    public IReadOnlyList<string> SitemapUrls { get; init; } = [];
    public decimal CrawlabilityScore { get; init; }
    public decimal CategoryRelevanceScore { get; init; }
    public decimal HeuristicExtractabilityScore { get; init; }
    public decimal ExtractabilityScore { get; init; }
    public decimal CatalogLikelihoodScore { get; init; }
    public string? RepresentativeCategoryPageUrl { get; init; }
    public bool RepresentativeCategoryPageReachable { get; init; }
    public string? RepresentativeProductPageUrl { get; init; }
    public bool RepresentativeProductPageReachable { get; init; }
    public bool RuntimeExtractionCompatible { get; init; }
    public int RepresentativeRuntimeProductCount { get; init; }
    public int AutomationCategorySampleCount { get; init; }
    public int AutomationReachableCategorySampleCount { get; init; }
    public int AutomationProductSampleCount { get; init; }
    public int AutomationReachableProductSampleCount { get; init; }
    public int AutomationRuntimeCompatibleProductSampleCount { get; init; }
    public int AutomationStructuredProductEvidenceSampleCount { get; init; }
    public int AutomationTechnicalAttributeEvidenceSampleCount { get; init; }
    public bool StructuredProductEvidenceDetected { get; init; }
    public bool TechnicalAttributeEvidenceDetected { get; init; }
    public bool LlmAcceptedRepresentativeProductPage { get; init; }
    public bool LlmRejectedRepresentativeProductPage { get; init; }
    public bool LlmDisagreedWithHeuristics { get; init; }
    public bool LlmDetectedSpecifications { get; init; }
    public string? LlmDetectedCategory { get; init; }
    public decimal? LlmConfidenceScore { get; init; }
    public string? LlmReason { get; init; }
    public bool NonCatalogContentHeavy { get; init; }
    public IReadOnlyList<string> CategoryPageHints { get; init; } = [];
    public IReadOnlyList<string> LikelyListingUrlPatterns { get; init; } = [];
    public IReadOnlyList<string> LikelyProductUrlPatterns { get; init; } = [];
}