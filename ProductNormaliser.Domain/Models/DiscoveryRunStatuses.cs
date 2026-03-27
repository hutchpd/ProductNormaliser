namespace ProductNormaliser.Core.Models;

public static class DiscoveryRunStatuses
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Paused = "paused";
    public const string CancelRequested = "cancel_requested";
    public const string Recoverable = "recoverable";
    public const string Cancelled = "cancelled";
    public const string Completed = "completed";
    public const string Failed = "failed";
}