namespace ProductNormaliser.AdminApi.Contracts;

public sealed class SourceQualitySnapshotDto
{
    public string SourceName { get; init; } = default!;
    public string CategoryKey { get; init; } = default!;
    public DateTime TimestampUtc { get; init; }
    public decimal AttributeCoverage { get; init; }
    public decimal ConflictRate { get; init; }
    public decimal AgreementRate { get; init; }
    public decimal SuccessfulCrawlRate { get; init; }
    public decimal ExtractabilityRate { get; init; }
    public decimal NoProductRate { get; init; }
    public decimal PriceVolatilityScore { get; init; }
    public decimal SpecStabilityScore { get; init; }
    public decimal HistoricalTrustScore { get; init; }
}