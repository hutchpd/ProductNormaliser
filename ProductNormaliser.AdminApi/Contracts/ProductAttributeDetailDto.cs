namespace ProductNormaliser.AdminApi.Contracts;

public sealed class ProductAttributeDetailDto
{
    public string AttributeKey { get; init; } = default!;
    public object? Value { get; init; }
    public string ValueType { get; init; } = default!;
    public string? Unit { get; init; }
    public decimal Confidence { get; init; }
    public bool HasConflict { get; init; }
    public IReadOnlyCollection<AttributeEvidenceDto> Evidence { get; init; } = [];
}