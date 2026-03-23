namespace ProductNormaliser.Infrastructure.Discovery;

public sealed class DiscoveryProcessResult
{
    public string Status { get; init; } = default!;
    public string Message { get; init; } = string.Empty;

    public static DiscoveryProcessResult Completed(string message)
    {
        return new DiscoveryProcessResult
        {
            Status = "completed",
            Message = message
        };
    }

    public static DiscoveryProcessResult Skipped(string message)
    {
        return new DiscoveryProcessResult
        {
            Status = "skipped",
            Message = message
        };
    }

    public static DiscoveryProcessResult Failed(string message)
    {
        return new DiscoveryProcessResult
        {
            Status = "failed",
            Message = message
        };
    }
}