namespace ProductNormaliser.AdminApi.Contracts;

public sealed class SourceDto
{
    public string SourceId { get; init; } = default!;
    public string DisplayName { get; init; } = default!;
    public string BaseUrl { get; init; } = default!;
    public string Host { get; init; } = default!;
    public string? Description { get; init; }
    public bool IsEnabled { get; init; }
    public IReadOnlyList<string> AllowedMarkets { get; init; } = [];
    public string PreferredLocale { get; init; } = string.Empty;
    public IReadOnlyList<string> SupportedCategoryKeys { get; init; } = [];
    public SourceDiscoveryProfileDto DiscoveryProfile { get; init; } = new();
    public SourceThrottlingPolicyDto ThrottlingPolicy { get; init; } = default!;
    public SourceReadinessDto Readiness { get; init; } = default!;
    public SourceHealthSummaryDto Health { get; init; } = default!;
    public SourceLastActivityDto? LastActivity { get; init; }
    public int DiscoveryQueueDepth { get; init; }
    public int ListingPagesVisitedLast24Hours { get; init; }
    public int SitemapUrlsProcessedLast24Hours { get; init; }
    public int ConfirmedProductUrlsLast24Hours { get; init; }
    public IReadOnlyDictionary<string, decimal> DiscoveryCoverageByCategory { get; init; } = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
    public DateTime? LastDiscoveryUtc { get; init; }
    public bool SitemapReachable { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime UpdatedUtc { get; init; }
}