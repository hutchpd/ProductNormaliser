namespace ProductNormaliser.Core.Models;

public sealed class AttributeEvidence
{
    public string SourceName { get; set; } = default!;
    public string SourceUrl { get; set; } = default!;
    public string SourceProductId { get; set; } = default!;
    public string SourceAttributeKey { get; set; } = default!;
    public string? RawValue { get; set; }
    public string? SelectorOrPath { get; set; }
    public decimal Confidence { get; set; }
    public DateTime ObservedUtc { get; set; }
}