namespace ProductNormaliser.Core.Models;

public sealed class CanonicalAttributeValue
{
    public string AttributeKey { get; set; } = default!;
    public object? Value { get; set; }
    public string ValueType { get; set; } = default!;
    public string? Unit { get; set; }
    public decimal Confidence { get; set; }
    public bool HasConflict { get; set; }

    public List<AttributeEvidence> Evidence { get; set; } = [];
}