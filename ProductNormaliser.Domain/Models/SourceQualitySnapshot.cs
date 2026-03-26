namespace ProductNormaliser.Core.Models;

public sealed class SourceQualitySnapshot
{
    public string Id { get; set; } = default!;
    public string SourceName { get; set; } = default!;
    public string CategoryKey { get; set; } = default!;
    public DateTime TimestampUtc { get; set; }
    public decimal AttributeCoverage { get; set; }
    public decimal ConflictRate { get; set; }
    public decimal AgreementRate { get; set; }
    public decimal SuccessfulCrawlRate { get; set; }
    public decimal ExtractabilityRate { get; set; }
    public decimal NoProductRate { get; set; }
    public decimal DiscoveryBreadthScore { get; set; }
    public decimal ProductTargetPromotionRate { get; set; }
    public decimal DownstreamYieldScore { get; set; }
    public decimal PriceVolatilityScore { get; set; }
    public decimal SpecStabilityScore { get; set; }
    public decimal HistoricalTrustScore { get; set; }
}