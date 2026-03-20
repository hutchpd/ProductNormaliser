namespace ProductNormaliser.AdminApi.Contracts;

public sealed class CrawlLogDto
{
    public string Id { get; init; } = default!;
    public string SourceName { get; init; } = default!;
    public string Url { get; init; } = default!;
    public string Status { get; init; } = default!;
    public long DurationMs { get; init; }
    public string? ContentHash { get; init; }
    public int ExtractedProductCount { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime TimestampUtc { get; init; }
}