namespace ProductNormaliser.AdminApi.Contracts;

public sealed class AttributeGapDto
{
    public string AttributeKey { get; init; } = default!;
    public string DisplayName { get; init; } = default!;
    public int ProductCount { get; init; }
    public decimal Percentage { get; init; }
}