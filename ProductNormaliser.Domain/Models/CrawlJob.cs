namespace ProductNormaliser.Core.Models;

public sealed class CrawlJob
{
    public string JobId { get; set; } = default!;
    public string RequestType { get; set; } = CrawlJobRequestTypes.Category;
    public IReadOnlyList<string> RequestedCategories { get; set; } = [];
    public IReadOnlyList<string> RequestedSources { get; set; } = [];
    public IReadOnlyList<string> RequestedProductIds { get; set; } = [];
    public int TotalTargets { get; set; }
    public int ProcessedTargets { get; set; }
    public int SuccessCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public int CancelledCount { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public DateTime? EstimatedCompletion { get; set; }
    public string Status { get; set; } = CrawlJobStatuses.Pending;
    public List<CrawlJobCategoryBreakdown> PerCategoryBreakdown { get; set; } = [];
}