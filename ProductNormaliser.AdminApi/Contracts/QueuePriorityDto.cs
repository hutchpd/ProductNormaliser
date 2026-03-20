namespace ProductNormaliser.AdminApi.Contracts;

public sealed class QueuePriorityDto
{
    public string Id { get; init; } = default!;
    public string SourceName { get; init; } = default!;
    public string SourceUrl { get; init; } = default!;
    public string CategoryKey { get; init; } = default!;
    public decimal PriorityScore { get; init; }
    public decimal SourceQualityScore { get; init; }
    public decimal ChangeFrequencyScore { get; init; }
    public decimal PriceVolatilityScore { get; init; }
    public decimal SpecStabilityScore { get; init; }
    public decimal MissingAttributeScore { get; init; }
    public decimal StalenessScore { get; init; }
    public int MissingAttributeCount { get; init; }
    public DateTime? NextAttemptUtc { get; init; }
    public DateTime EnqueuedUtc { get; init; }
    public DateTime? LastCrawledUtc { get; init; }
    public IReadOnlyList<string> Reasons { get; init; } = [];
}