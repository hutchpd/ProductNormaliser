namespace ProductNormaliser.Core.Models;

public static class CrawlJobStatuses
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string CancelRequested = "cancel_requested";
    public const string Cancelled = "cancelled";
    public const string Completed = "completed";
    public const string CompletedWithFailures = "completed_with_failures";
    public const string Failed = "failed";
}