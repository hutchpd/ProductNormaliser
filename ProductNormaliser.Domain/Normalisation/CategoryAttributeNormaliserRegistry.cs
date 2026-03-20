using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Normalisation;

public sealed class CategoryAttributeNormaliserRegistry(IEnumerable<ICategoryAttributeNormaliser> providers)
    : ICategoryAttributeNormaliserRegistry, IAttributeNormaliser
{
    private static readonly string[] NoKeys = [];

    private readonly IReadOnlyDictionary<string, ICategoryAttributeNormaliser> providersByCategoryKey = providers
        .GroupBy(provider => provider.SupportedCategoryKey, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

    public ICategoryAttributeNormaliser? GetProvider(string categoryKey)
    {
        if (string.IsNullOrWhiteSpace(categoryKey))
        {
            return null;
        }

        return providersByCategoryKey.TryGetValue(categoryKey, out var provider)
            ? provider
            : null;
    }

    public IReadOnlyCollection<string> GetIdentityAttributeKeys(string categoryKey)
    {
        return GetProvider(categoryKey)?.IdentityAttributeKeys ?? NoKeys;
    }

    public IReadOnlyCollection<string> GetCompletenessAttributeKeys(string categoryKey)
    {
        return GetProvider(categoryKey)?.CompletenessAttributeKeys ?? NoKeys;
    }

    public Dictionary<string, NormalisedAttributeValue> Normalise(string categoryKey, Dictionary<string, SourceAttributeValue> rawAttributes)
    {
        return GetProvider(categoryKey)?.Normalise(rawAttributes) ?? [];
    }
}