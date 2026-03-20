using System.ComponentModel.DataAnnotations;

namespace ProductNormaliser.AdminApi.Contracts;

public sealed class UpdateSourceThrottlingRequest
{
    [Range(0, int.MaxValue)]
    public int MinDelayMs { get; init; }

    [Range(0, int.MaxValue)]
    public int MaxDelayMs { get; init; }

    [Range(1, int.MaxValue)]
    public int MaxConcurrentRequests { get; init; }

    [Range(1, int.MaxValue)]
    public int RequestsPerMinute { get; init; }

    public bool RespectRobotsTxt { get; init; } = true;
}