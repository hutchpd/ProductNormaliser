namespace ProductNormaliser.AdminApi.Contracts;

public sealed class CategorySchemaAttributeDto
{
    public string Key { get; init; } = default!;
    public string DisplayName { get; init; } = default!;
    public string ValueType { get; init; } = default!;
    public string? Unit { get; init; }
    public bool IsRequired { get; init; }
    public string Description { get; init; } = default!;
}