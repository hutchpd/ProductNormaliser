namespace ProductNormaliser.Core.Models;

public sealed class CanonicalAttributeValue
{
    public string AttributeKey { get; set; } = default!;
    public object? Value { get; set; }
    public string ValueType { get; set; } = default!;
    public string? Unit { get; set; }
    public decimal Confidence { get; set; }
    public bool HasConflict { get; set; }
    public decimal MergeWeight { get; set; }
    public decimal ReliabilityScore { get; set; }
    public decimal SourceQualityScore { get; set; }
    public string? WinningSourceName { get; set; }
    public DateTime LastObservedUtc { get; set; }

    public List<AttributeEvidence> Evidence { get; set; } = [];
}