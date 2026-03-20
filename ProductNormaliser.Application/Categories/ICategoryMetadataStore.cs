using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Categories;

public interface ICategoryMetadataStore
{
    Task<CategoryMetadata?> GetAsync(string categoryKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryMetadata>> ListAsync(CancellationToken cancellationToken = default);
    Task UpsertAsync(CategoryMetadata categoryMetadata, CancellationToken cancellationToken = default);
}