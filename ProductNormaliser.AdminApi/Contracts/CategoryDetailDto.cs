namespace ProductNormaliser.AdminApi.Contracts;

public sealed class CategoryDetailDto
{
    public CategoryMetadataDto Metadata { get; init; } = default!;
    public CategorySchemaDto Schema { get; init; } = default!;
}