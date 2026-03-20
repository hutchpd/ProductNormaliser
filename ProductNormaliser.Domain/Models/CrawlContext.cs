namespace ProductNormaliser.Core.Models;

public sealed class CrawlContext
{
    public string SourceName { get; set; } = default!;
    public string CategoryKey { get; set; } = default!;
    public string SourceUrl { get; set; } = default!;
    public decimal ImportanceScore { get; set; }
    public int ConsecutiveFailureCount { get; set; }
    public DateTime? LastAttemptUtc { get; set; }
    public DateTime UtcNow { get; set; }
}