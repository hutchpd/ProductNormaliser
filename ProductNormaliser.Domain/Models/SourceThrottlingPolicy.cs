namespace ProductNormaliser.Core.Models;

public sealed class SourceThrottlingPolicy
{
    public int MinDelayMs { get; set; } = 1000;
    public int MaxDelayMs { get; set; } = 3000;
    public int MaxConcurrentRequests { get; set; } = 1;
    public int RequestsPerMinute { get; set; } = 30;
    public bool RespectRobotsTxt { get; set; } = true;
}