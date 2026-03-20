using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Interfaces;

public interface IAttributeStabilityService
{
    decimal GetStabilityScore(string categoryKey, string attributeKey);
    IReadOnlyList<AttributeStabilityScore> GetScores(string categoryKey);
}