using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Categories;

public sealed class CategoryDetail
{
    public CategoryMetadata Metadata { get; init; } = default!;
    public CategorySchema Schema { get; init; } = default!;
}