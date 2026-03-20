namespace ProductNormaliser.AdminApi.Contracts;

public sealed class CategorySchemaDto
{
    public string CategoryKey { get; init; } = default!;
    public string DisplayName { get; init; } = default!;
    public IReadOnlyList<CategorySchemaAttributeDto> Attributes { get; init; } = [];
}