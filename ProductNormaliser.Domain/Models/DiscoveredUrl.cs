namespace ProductNormaliser.Core.Models;

public sealed class DiscoveredUrl
{
    public string Id { get; set; } = default!;
    public string? JobId { get; set; }
    public string SourceId { get; set; } = default!;
    public string CategoryKey { get; set; } = default!;
    public string Url { get; set; } = default!;
    public string NormalizedUrl { get; set; } = default!;
    public string Classification { get; set; } = default!;
    public string State { get; set; } = default!;
    public string? ParentUrl { get; set; }
    public int Depth { get; set; }
    public int AttemptCount { get; set; }
    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public DateTime? LastProcessedUtc { get; set; }
    public DateTime? NextAttemptUtc { get; set; }
    public DateTime? PromotedToCrawlUtc { get; set; }
    public string? LastError { get; set; }
}