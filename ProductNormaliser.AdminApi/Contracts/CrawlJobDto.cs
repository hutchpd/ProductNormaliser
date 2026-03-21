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
    public DateTime StartedAt { get; init; }
    public DateTime LastUpdatedAt { get; init; }
    public DateTime? EstimatedCompletion { get; init; }
    public string Status { get; init; } = string.Empty;
    public IReadOnlyList<CrawlJobCategoryBreakdownDto> PerCategoryBreakdown { get; init; } = [];
}