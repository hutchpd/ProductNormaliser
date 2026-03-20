using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Categories;

public interface ICategoryMetadataService
{
    Task EnsureDefaultsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryMetadata>> ListAsync(bool enabledOnly = false, CancellationToken cancellationToken = default);
    Task<CategoryMetadata?> GetAsync(string categoryKey, CancellationToken cancellationToken = default);
    Task<CategoryMetadata> UpsertAsync(CategoryMetadata categoryMetadata, CancellationToken cancellationToken = default);
}