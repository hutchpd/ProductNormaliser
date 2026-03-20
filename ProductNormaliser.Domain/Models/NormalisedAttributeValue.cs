namespace ProductNormaliser.Core.Models;

public sealed class NormalisedAttributeValue
{
    public string AttributeKey { get; set; } = default!;
    public object? Value { get; set; }
    public string ValueType { get; set; } = default!;
    public string? Unit { get; set; }
    public decimal Confidence { get; set; }
    public string? SourceAttributeKey { get; set; }
    public string? OriginalValue { get; set; }
    public string? ParseNotes { get; set; }
}