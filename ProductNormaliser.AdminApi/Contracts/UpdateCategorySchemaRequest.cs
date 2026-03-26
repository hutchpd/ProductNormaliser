namespace ProductNormaliser.AdminApi.Contracts;

public sealed class UpdateCategorySchemaRequest
{
    public IReadOnlyList<CategorySchemaAttributeDto> Attributes { get; init; } = [];
}