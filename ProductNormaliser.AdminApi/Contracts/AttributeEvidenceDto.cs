namespace ProductNormaliser.AdminApi.Contracts;

public sealed class AttributeEvidenceDto
{
    public string SourceName { get; init; } = default!;
    public string SourceUrl { get; init; } = default!;
    public string SourceProductId { get; init; } = default!;
    public string SourceAttributeKey { get; init; } = default!;
    public string? RawValue { get; init; }
    public string? SelectorOrPath { get; init; }
    public decimal Confidence { get; init; }
    public DateTime ObservedUtc { get; init; }
}