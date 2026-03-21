namespace ProductNormaliser.Core.Models;

public sealed class CrawlJobCategoryBreakdown
{
    public string CategoryKey { get; set; } = default!;
    public int TotalTargets { get; set; }
    public int ProcessedTargets { get; set; }
    public int SuccessCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public int CancelledCount { get; set; }
}