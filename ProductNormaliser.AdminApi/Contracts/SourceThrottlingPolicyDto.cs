namespace ProductNormaliser.AdminApi.Contracts;

public sealed class SourceThrottlingPolicyDto
{
    public int MinDelayMs { get; init; }
    public int MaxDelayMs { get; init; }
    public int MaxConcurrentRequests { get; init; }
    public int RequestsPerMinute { get; init; }
    public bool RespectRobotsTxt { get; init; }
}