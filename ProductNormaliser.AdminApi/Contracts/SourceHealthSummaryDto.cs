namespace ProductNormaliser.AdminApi.Contracts;

public sealed class SourceHealthSummaryDto
{
    public string Status { get; init; } = default!;
    public decimal TrustScore { get; init; }
    public decimal CoveragePercent { get; init; }
    public decimal SuccessfulCrawlRate { get; init; }
    public DateTime? SnapshotUtc { get; init; }
}