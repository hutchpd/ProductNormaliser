namespace ProductNormaliser.Core.Models;

public sealed class MergeConflict
{
    public string Id { get; set; } = default!;
    public string CanonicalProductId { get; set; } = default!;
    public string AttributeKey { get; set; } = default!;
    public object? ExistingValue { get; set; }
    public object? IncomingValue { get; set; }
    public string Reason { get; set; } = default!;
    public decimal Severity { get; set; }
    public string Status { get; set; } = default!;
    public object? SuggestedValue { get; set; }
    public string? SuggestedSourceName { get; set; }
    public decimal SuggestedConfidence { get; set; }
    public object? HighestConfidenceValue { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? ResolvedUtc { get; set; }
}