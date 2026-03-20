namespace ProductNormaliser.AdminApi.Contracts;

public sealed class CategoryFamilyDto
{
    public string FamilyKey { get; init; } = default!;
    public string FamilyDisplayName { get; init; } = default!;
    public IReadOnlyList<CategoryMetadataDto> Categories { get; init; } = [];
}