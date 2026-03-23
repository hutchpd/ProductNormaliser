using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Interfaces;

public interface ISourceDisagreementService
{
    decimal GetSourceAttributeAdjustment(string sourceName, string categoryKey, string attributeKey);
    IReadOnlyList<SourceAttributeDisagreement> GetDisagreements(string categoryKey, string? sourceName = null, int? timeRangeDays = null);
    void RefreshForProduct(CanonicalProduct product);
}