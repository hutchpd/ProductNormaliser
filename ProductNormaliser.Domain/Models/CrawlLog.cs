namespace ProductNormaliser.Core.Models;

public sealed class CrawlLog
{
    public string Id { get; set; } = default!;
    public string SourceName { get; set; } = default!;
    public string Url { get; set; } = default!;
    public string Status { get; set; } = default!;
    public long DurationMs { get; set; }
    public string? ContentHash { get; set; }
    public int ExtractedProductCount { get; set; }
    public bool HadMeaningfulChange { get; set; }
    public string? MeaningfulChangeSummary { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime TimestampUtc { get; set; }
}