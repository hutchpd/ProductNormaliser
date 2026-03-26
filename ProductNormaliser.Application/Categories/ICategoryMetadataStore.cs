using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Categories;

public interface ICategoryMetadataStore
{
    CategoryMetadata? Get(string categoryKey);
    Task<CategoryMetadata?> GetAsync(string categoryKey, CancellationToken cancellationToken = default);
    IReadOnlyList<CategoryMetadata> List();
    Task<IReadOnlyList<CategoryMetadata>> ListAsync(CancellationToken cancellationToken = default);
    Task UpsertAsync(CategoryMetadata categoryMetadata, CancellationToken cancellationToken = default);
}