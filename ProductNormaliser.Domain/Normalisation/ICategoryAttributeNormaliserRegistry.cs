using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Normalisation;

public interface ICategoryAttributeNormaliserRegistry
{
    ICategoryAttributeNormaliser? GetProvider(string categoryKey);
    IReadOnlyCollection<string> GetIdentityAttributeKeys(string categoryKey);
    IReadOnlyCollection<string> GetCompletenessAttributeKeys(string categoryKey);
    Dictionary<string, NormalisedAttributeValue> Normalise(string categoryKey, Dictionary<string, SourceAttributeValue> rawAttributes);
}