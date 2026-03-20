namespace ProductNormaliser.Worker;

public sealed class CrawlProcessResult
{
    public string Status { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;

    public static CrawlProcessResult Completed(string message) => new() { Status = "completed", Message = message };
    public static CrawlProcessResult Skipped(string message) => new() { Status = "skipped", Message = message };
    public static CrawlProcessResult Failed(string message) => new() { Status = "failed", Message = message };
}