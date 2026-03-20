namespace ProductNormaliser.AdminApi.Contracts;

public sealed class QueueItemDto
{
    public string Id { get; init; } = default!;
    public string SourceName { get; init; } = default!;
    public string SourceUrl { get; init; } = default!;
    public string CategoryKey { get; init; } = default!;
    public string Status { get; init; } = default!;
    public int AttemptCount { get; init; }
    public int ConsecutiveFailureCount { get; init; }
    public decimal ImportanceScore { get; init; }
    public DateTime EnqueuedUtc { get; init; }
    public DateTime? LastAttemptUtc { get; init; }
    public DateTime? NextAttemptUtc { get; init; }
    public string? LastError { get; init; }
}