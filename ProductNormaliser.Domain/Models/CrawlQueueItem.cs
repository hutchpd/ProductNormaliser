namespace ProductNormaliser.Core.Models;

public sealed class CrawlQueueItem
{
    public string Id { get; set; } = default!;
    public string? JobId { get; set; }
    public string? InitiatingJobId { get; set; }
    public string SourceName { get; set; } = default!;
    public string SourceUrl { get; set; } = default!;
    public string CategoryKey { get; set; } = default!;
    public string Status { get; set; } = default!;
    public int AttemptCount { get; set; }
    public int ConsecutiveFailureCount { get; set; }
    public decimal ImportanceScore { get; set; } = 0.50m;
    public DateTime EnqueuedUtc { get; set; }
    public DateTime? LastAttemptUtc { get; set; }
    public DateTime? NextAttemptUtc { get; set; }
    public string? LastError { get; set; }
}