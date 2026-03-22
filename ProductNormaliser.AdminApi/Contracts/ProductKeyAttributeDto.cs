namespace ProductNormaliser.AdminApi.Contracts;

public sealed class ProductKeyAttributeDto
{
    public string AttributeKey { get; init; } = default!;
    public string DisplayName { get; init; } = default!;
    public string Value { get; init; } = default!;
    public bool HasConflict { get; init; }
    public decimal Confidence { get; init; }
}