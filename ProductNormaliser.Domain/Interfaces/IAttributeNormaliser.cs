using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Interfaces;

public interface IAttributeNormaliser
{
    Dictionary<string, NormalisedAttributeValue> Normalise(
        string categoryKey,
        Dictionary<string, SourceAttributeValue> rawAttributes);
}