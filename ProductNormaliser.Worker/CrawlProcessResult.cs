namespace ProductNormaliser.Worker;

public sealed class CrawlProcessResult
{
    public const string ExtractionOutcomeExtracted = "products_extracted";
    public const string ExtractionOutcomeNoProducts = "no_products";
    public const string ExtractionOutcomeNotAttempted = "not_attempted";

    public string Status { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? ContentHash { get; init; }
    public int ExtractedProductCount { get; init; }
    public string ExtractionOutcome { get; init; } = ExtractionOutcomeNotAttempted;

    public static CrawlProcessResult Completed(string message, string? contentHash = null, int extractedProductCount = 0, string extractionOutcome = ExtractionOutcomeNotAttempted)
        => new() { Status = "completed", Message = message, ContentHash = contentHash, ExtractedProductCount = extractedProductCount, ExtractionOutcome = extractionOutcome };

    public static CrawlProcessResult Skipped(string message, string? contentHash = null, int extractedProductCount = 0, string extractionOutcome = ExtractionOutcomeNotAttempted)
        => new() { Status = "skipped", Message = message, ContentHash = contentHash, ExtractedProductCount = extractedProductCount, ExtractionOutcome = extractionOutcome };

    public static CrawlProcessResult Failed(string message, string? contentHash = null, int extractedProductCount = 0, string extractionOutcome = ExtractionOutcomeNotAttempted)
        => new() { Status = "failed", Message = message, ContentHash = contentHash, ExtractedProductCount = extractedProductCount, ExtractionOutcome = extractionOutcome };
}