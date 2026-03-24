namespace ProductNormaliser.AdminApi.Contracts;

public sealed class CrawlJobDto
{
    public string JobId { get; init; } = string.Empty;
    public string RequestType { get; init; } = string.Empty;
    public IReadOnlyList<string> RequestedCategories { get; init; } = [];
    public IReadOnlyList<string> RequestedSources { get; init; } = [];
    public IReadOnlyList<string> RequestedProductIds { get; init; } = [];
    public int TotalTargets { get; init; }
    public int ProcessedTargets { get; init; }
    public int SuccessCount { get; init; }
    public int SkippedCount { get; init; }
    public int FailedCount { get; init; }
    public int CancelledCount { get; init; }
    public int DiscoveredUrlCount { get; init; }
    public int ConfirmedProductTargetCount { get; init; }
    public int RejectedPageCount { get; init; }
    public int BlockedPageCount { get; init; }
    public int DiscoveryQueueDepth { get; init; }
    public int ActiveSourceCoverage { get; init; }
    public decimal SourceCoveragePercent { get; init; }
    public decimal DiscoveryCompletionPercent { get; init; }
    public int CrawledProductUrlCount { get; init; }
    public int ProductQueueDepth { get; init; }
    public int ProductFailureCount { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime LastUpdatedAt { get; init; }
    public DateTime? EstimatedCompletion { get; init; }
    public string Status { get; init; } = string.Empty;
    public IReadOnlyList<CrawlJobCategoryBreakdownDto> PerCategoryBreakdown { get; init; } = [];
}