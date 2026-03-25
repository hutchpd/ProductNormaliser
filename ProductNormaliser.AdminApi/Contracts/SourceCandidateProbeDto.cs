namespace ProductNormaliser.AdminApi.Contracts;

public sealed class SourceCandidateProbeDto
{
    public bool HomePageReachable { get; init; }
    public bool RobotsTxtReachable { get; init; }
    public bool SitemapDetected { get; init; }
    public IReadOnlyList<string> SitemapUrls { get; init; } = [];
    public decimal CrawlabilityScore { get; init; }
    public decimal CategoryRelevanceScore { get; init; }
    public decimal ExtractabilityScore { get; init; }
    public decimal CatalogLikelihoodScore { get; init; }
    public string? RepresentativeCategoryPageUrl { get; init; }
    public bool RepresentativeCategoryPageReachable { get; init; }
    public string? RepresentativeProductPageUrl { get; init; }
    public bool RepresentativeProductPageReachable { get; init; }
    public bool StructuredProductEvidenceDetected { get; init; }
    public bool TechnicalAttributeEvidenceDetected { get; init; }
    public bool NonCatalogContentHeavy { get; init; }
    public IReadOnlyList<string> CategoryPageHints { get; init; } = [];
    public IReadOnlyList<string> LikelyListingUrlPatterns { get; init; } = [];
    public IReadOnlyList<string> LikelyProductUrlPatterns { get; init; } = [];
}