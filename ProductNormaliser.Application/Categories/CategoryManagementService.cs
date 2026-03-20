using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Application.Categories;

public sealed class CategoryManagementService(
    ICategoryMetadataService categoryMetadataService,
    ICategorySchemaRegistry categorySchemaRegistry) : ICategoryManagementService
{
    public Task<IReadOnlyList<CategoryMetadata>> ListAsync(CancellationToken cancellationToken = default)
    {
        return categoryMetadataService.ListAsync(enabledOnly: false, cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryMetadata>> ListEnabledAsync(CancellationToken cancellationToken = default)
    {
        return (await categoryMetadataService.ListAsync(enabledOnly: true, cancellationToken))
            .Where(IsCrawlable)
            .OrderBy(category => category.FamilyDisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(category => category.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<CategoryFamily>> ListFamiliesAsync(CancellationToken cancellationToken = default)
    {
        var categories = await ListAsync(cancellationToken);
        return categories
            .GroupBy(category => new { category.FamilyKey, category.FamilyDisplayName })
            .Select(group => new CategoryFamily
            {
                FamilyKey = group.Key.FamilyKey,
                FamilyDisplayName = group.Key.FamilyDisplayName,
                Categories = group
                    .OrderBy(category => category.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            })
            .OrderBy(family => family.FamilyDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public Task<CategoryMetadata?> GetAsync(string categoryKey, CancellationToken cancellationToken = default)
    {
        return categoryMetadataService.GetAsync(categoryKey, cancellationToken);
    }

    public async Task<CategoryDetail?> GetDetailAsync(string categoryKey, CancellationToken cancellationToken = default)
    {
        var metadata = await GetAsync(categoryKey, cancellationToken);
        if (metadata is null)
        {
            return null;
        }

        var schema = categorySchemaRegistry.GetSchema(categoryKey);
        return schema is null
            ? null
            : new CategoryDetail
            {
                Metadata = metadata,
                Schema = schema
            };
    }

    public async Task<CategorySchema?> GetSchemaAsync(string categoryKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryKey);

        var category = await categoryMetadataService.GetAsync(categoryKey, cancellationToken);
        return category is null ? null : categorySchemaRegistry.GetSchema(categoryKey);
    }

    public Task<CategoryMetadata> UpsertAsync(CategoryMetadata categoryMetadata, CancellationToken cancellationToken = default)
    {
        return categoryMetadataService.UpsertAsync(categoryMetadata, cancellationToken);
    }

    private static bool IsCrawlable(CategoryMetadata category)
    {
        return category.IsEnabled
            && category.CrawlSupportStatus is CrawlSupportStatus.Supported or CrawlSupportStatus.Experimental;
    }
}