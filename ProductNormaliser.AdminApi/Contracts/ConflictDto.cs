namespace ProductNormaliser.AdminApi.Contracts;

public sealed class ConflictDto
{
    public string Id { get; init; } = default!;
    public string CanonicalProductId { get; init; } = default!;
    public string AttributeKey { get; init; } = default!;
    public object? ExistingValue { get; init; }
    public object? IncomingValue { get; init; }
    public string Reason { get; init; } = default!;
    public decimal Severity { get; init; }
    public string Status { get; init; } = default!;
    public object? SuggestedValue { get; init; }
    public string? SuggestedSourceName { get; init; }
    public decimal SuggestedConfidence { get; init; }
    public object? HighestConfidenceValue { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime? ResolvedUtc { get; init; }
}