using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Categories;

public interface ICategoryManagementService
{
    Task<IReadOnlyList<CategoryMetadata>> ListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryMetadata>> ListEnabledAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryFamily>> ListFamiliesAsync(CancellationToken cancellationToken = default);
    Task<CategoryMetadata?> GetAsync(string categoryKey, CancellationToken cancellationToken = default);
    Task<CategoryDetail?> GetDetailAsync(string categoryKey, CancellationToken cancellationToken = default);
    Task<CategorySchema?> GetSchemaAsync(string categoryKey, CancellationToken cancellationToken = default);
    Task<CategoryMetadata> UpsertAsync(CategoryMetadata categoryMetadata, CancellationToken cancellationToken = default);
    Task<CategorySchema?> UpdateSchemaAsync(string categoryKey, IReadOnlyList<CanonicalAttributeDefinition> attributes, CancellationToken cancellationToken = default);
}