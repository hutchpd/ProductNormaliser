namespace ProductNormaliser.Worker;

public sealed class CrawlProcessResult
{
    public string Status { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? ContentHash { get; init; }
    public int ExtractedProductCount { get; init; }

    public static CrawlProcessResult Completed(string message, string? contentHash = null, int extractedProductCount = 0) => new() { Status = "completed", Message = message, ContentHash = contentHash, ExtractedProductCount = extractedProductCount };
    public static CrawlProcessResult Skipped(string message, string? contentHash = null, int extractedProductCount = 0) => new() { Status = "skipped", Message = message, ContentHash = contentHash, ExtractedProductCount = extractedProductCount };
    public static CrawlProcessResult Failed(string message, string? contentHash = null, int extractedProductCount = 0) => new() { Status = "failed", Message = message, ContentHash = contentHash, ExtractedProductCount = extractedProductCount };
}