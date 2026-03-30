namespace ProductNormaliser.AdminApi.Contracts;

public sealed class SourceCandidateProbeDto
{
    public bool HomePageReachable { get; init; }
    public bool RobotsTxtReachable { get; init; }
    public bool ProbeTimedOut { get; init; }
    public bool ProbeFailed { get; init; }
    public bool SitemapDetected { get; init; }
    public IReadOnlyList<string> SitemapUrls { get; init; } = [];
    public decimal CrawlabilityScore { get; init; }
    public decimal CategoryRelevanceScore { get; init; }
    public decimal ExtractabilityScore { get; init; }
    public decimal CatalogLikelihoodScore { get; init; }
    public string? RepresentativeCategoryPageUrl { get; init; }
    public bool RepresentativeCategoryPageReachable { get; init; }
    public bool RepresentativeCategoryPageFetchFailed { get; init; }
    public string? RepresentativeProductPageUrl { get; init; }
    public bool RepresentativeProductPageReachable { get; init; }
    public bool RepresentativeProductPageFetchFailed { get; init; }
    public bool RuntimeExtractionCompatible { get; init; }
    public int RepresentativeRuntimeProductCount { get; init; }
    public int ProbeAttemptCount { get; init; }
    public long ProbeElapsedMs { get; init; }
    public long? LlmElapsedMs { get; init; }
    public long? LlmBudgetMs { get; init; }
    public bool LlmBudgetLimitedByProbe { get; init; }
    public bool LlmTimedOut { get; init; }
    public bool StructuredProductEvidenceDetected { get; init; }
    public bool TechnicalAttributeEvidenceDetected { get; init; }
    public bool NonCatalogContentHeavy { get; init; }
    public IReadOnlyList<string> CategoryPageHints { get; init; } = [];
    public IReadOnlyList<string> LikelyListingUrlPatterns { get; init; } = [];
    public IReadOnlyList<string> LikelyProductUrlPatterns { get; init; } = [];
}