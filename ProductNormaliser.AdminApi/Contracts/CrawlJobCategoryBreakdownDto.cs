namespace ProductNormaliser.AdminApi.Contracts;

public sealed class CrawlJobCategoryBreakdownDto
{
    public string CategoryKey { get; init; } = string.Empty;
    public int TotalTargets { get; init; }
    public int ProcessedTargets { get; init; }
    public int SuccessCount { get; init; }
    public int SkippedCount { get; init; }
    public int FailedCount { get; init; }
    public int CancelledCount { get; init; }
}