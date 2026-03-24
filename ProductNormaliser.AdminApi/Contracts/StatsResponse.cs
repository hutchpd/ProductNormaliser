namespace ProductNormaliser.AdminApi.Contracts;

public sealed class StatsResponse
{
    public int TotalCanonicalProducts { get; init; }
    public int TotalSourceProducts { get; init; }
    public decimal AverageAttributesPerProduct { get; init; }
    public decimal PercentProductsWithConflicts { get; init; }
    public decimal PercentProductsMissingKeyAttributes { get; init; }
    public int DiscoveryQueueDepth { get; init; }
    public decimal DiscoveryProcessingRateLast24Hours { get; init; }
    public int DiscoveredUrlCountLast24Hours { get; init; }
    public int ConfirmedProductUrlCountLast24Hours { get; init; }
    public int RejectedUrlCountLast24Hours { get; init; }
    public int RobotsBlockedCountLast24Hours { get; init; }
    public int ActiveDiscoverySourceCount { get; init; }
    public OperationalSummaryDto Operational { get; init; } = new();
}

public sealed class OperationalSummaryDto
{
    public int ActiveJobCount { get; init; }
    public int QueueDepth { get; init; }
    public int RetryQueueDepth { get; init; }
    public int FailedQueueDepth { get; init; }
    public int ThroughputLast24Hours { get; init; }
    public int FailureCountLast24Hours { get; init; }
    public int HealthySourceCount { get; init; }
    public int AttentionSourceCount { get; init; }
    public IReadOnlyList<SourceOperationalMetricDto> Sources { get; init; } = [];
    public IReadOnlyList<CategoryOperationalMetricDto> Categories { get; init; } = [];
}

public sealed class SourceOperationalMetricDto
{
    public string SourceName { get; init; } = string.Empty;
    public string HealthStatus { get; init; } = string.Empty;
    public int QueueDepth { get; init; }
    public int DiscoveryQueueDepth { get; init; }
    public int RetryQueueDepth { get; init; }
    public int FailedQueueDepth { get; init; }
    public int TotalCrawlsLast24Hours { get; init; }
    public int FailedCrawlsLast24Hours { get; init; }
    public decimal FailureRateLast24Hours { get; init; }
    public int ListingPagesVisitedLast24Hours { get; init; }
    public int SitemapUrlsProcessedLast24Hours { get; init; }
    public int ConfirmedProductUrlsLast24Hours { get; init; }
    public IReadOnlyDictionary<string, decimal> DiscoveryCoverageByCategory { get; init; } = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
    public decimal TrustScore { get; init; }
    public decimal CoveragePercent { get; init; }
    public decimal SuccessfulCrawlRate { get; init; }
    public DateTime? SnapshotUtc { get; init; }
    public DateTime? LastCrawlUtc { get; init; }
    public DateTime? LastDiscoveryUtc { get; init; }
}

public sealed class CategoryOperationalMetricDto
{
    public string CategoryKey { get; init; } = string.Empty;
    public int ActiveJobCount { get; init; }
    public int QueueDepth { get; init; }
    public int RetryQueueDepth { get; init; }
    public int ThroughputLast24Hours { get; init; }
    public int CrawledProductUrlCountLast24Hours { get; init; }
    public int FailedCrawlsLast24Hours { get; init; }
    public decimal FailureRateLast24Hours { get; init; }
    public int DistinctSourceCount { get; init; }
    public int DiscoveredUrlCount { get; init; }
    public int ConfirmedProductTargetCount { get; init; }
    public int DiscoveryQueueDepth { get; init; }
    public int ActiveSourceCoverage { get; init; }
    public decimal SourceCoveragePercent { get; init; }
    public decimal DiscoveryCompletionPercent { get; init; }
}