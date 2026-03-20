using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Crawling;

public sealed class CrawlPriorityAssessment
{
    public CrawlQueueItem QueueItem { get; init; } = default!;
    public decimal PriorityScore { get; init; }
    public decimal SourceQualityScore { get; init; }
    public decimal ChangeFrequencyScore { get; init; }
    public decimal PriceVolatilityScore { get; init; }
    public decimal SpecStabilityScore { get; init; }
    public decimal MissingAttributeScore { get; init; }
    public decimal StalenessScore { get; init; }
    public int MissingAttributeCount { get; init; }
    public DateTime? LastCrawledUtc { get; init; }
    public IReadOnlyList<string> Reasons { get; init; } = [];
}