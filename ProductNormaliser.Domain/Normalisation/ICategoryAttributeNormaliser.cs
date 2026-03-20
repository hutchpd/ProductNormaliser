using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Normalisation;

public interface ICategoryAttributeNormaliser
{
    string SupportedCategoryKey { get; }
    IReadOnlyCollection<string> IdentityAttributeKeys { get; }
    IReadOnlyCollection<string> CompletenessAttributeKeys { get; }
    Dictionary<string, NormalisedAttributeValue> Normalise(Dictionary<string, SourceAttributeValue> rawAttributes);
}