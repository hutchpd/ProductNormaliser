using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Schemas;

public sealed class CategorySchemaRegistry(IEnumerable<ICategorySchemaProvider> providers) : ICategorySchemaRegistry
{
    private readonly IReadOnlyDictionary<string, ICategorySchemaProvider> providersByCategoryKey = providers
        .GroupBy(provider => provider.SupportedCategoryKey, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

    public ICategorySchemaProvider? GetProvider(string categoryKey)
    {
        if (string.IsNullOrWhiteSpace(categoryKey))
        {
            return null;
        }

        return providersByCategoryKey.TryGetValue(categoryKey, out var provider)
            ? provider
            : null;
    }

    public CategorySchema? GetSchema(string categoryKey)
    {
        return GetProvider(categoryKey)?.GetSchema();
    }

    public bool TryGetSchema(string categoryKey, out CategorySchema schema)
    {
        schema = default!;

        if (GetSchema(categoryKey) is not { } resolvedSchema)
        {
            return false;
        }

        schema = resolvedSchema;
        return true;
    }
}