namespace ProductNormaliser.Core.Models;

public sealed class AdaptiveCrawlPolicy
{
    public string Id { get; set; } = default!;
    public string SourceName { get; set; } = default!;
    public string CategoryKey { get; set; } = default!;
    public int MinIntervalMinutes { get; set; }
    public int MaxIntervalMinutes { get; set; }
    public decimal VolatilityMultiplier { get; set; }
    public decimal TrustMultiplier { get; set; }
    public decimal FailureBackoffFactor { get; set; }
    public DateTime LastComputedUtc { get; set; }
}