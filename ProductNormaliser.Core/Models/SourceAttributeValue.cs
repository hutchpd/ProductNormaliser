namespace ProductNormaliser.Core.Models;

public sealed class SourceAttributeValue
{
    public string AttributeKey { get; set; } = default!;
    public string? Value { get; set; }
    public string ValueType { get; set; } = default!;
    public string? Unit { get; set; }
    public string? SourcePath { get; set; }
}