namespace ProductNormaliser.AdminApi.Contracts;

public sealed class SourceCandidateProbeDto
{
    public bool HomePageReachable { get; init; }
    public bool RobotsTxtReachable { get; init; }
    public bool SitemapDetected { get; init; }
    public IReadOnlyList<string> SitemapUrls { get; init; } = [];
    public decimal CategoryRelevanceScore { get; init; }
    public IReadOnlyList<string> CategoryPageHints { get; init; } = [];
    public IReadOnlyList<string> LikelyListingUrlPatterns { get; init; } = [];
    public IReadOnlyList<string> LikelyProductUrlPatterns { get; init; } = [];
}