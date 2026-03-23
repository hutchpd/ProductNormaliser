namespace ProductNormaliser.Core.Models;

public sealed class DiscoveryQueueItem
{
    public string Id { get; set; } = default!;
    public string? JobId { get; set; }
    public string SourceId { get; set; } = default!;
    public string CategoryKey { get; set; } = default!;
    public string Url { get; set; } = default!;
    public string NormalizedUrl { get; set; } = default!;
    public string Classification { get; set; } = default!;
    public string State { get; set; } = default!;
    public int Depth { get; set; }
    public string? ParentUrl { get; set; }
    public int AttemptCount { get; set; }
    public DateTime EnqueuedUtc { get; set; }
    public DateTime? LastAttemptUtc { get; set; }
    public DateTime? NextAttemptUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public string? LastError { get; set; }
}