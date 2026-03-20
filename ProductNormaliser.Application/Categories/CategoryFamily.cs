using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Categories;

public sealed class CategoryFamily
{
    public string FamilyKey { get; init; } = default!;
    public string FamilyDisplayName { get; init; } = default!;
    public IReadOnlyList<CategoryMetadata> Categories { get; init; } = [];
}