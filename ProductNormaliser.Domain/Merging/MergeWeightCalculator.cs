using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Normalisation;

namespace ProductNormaliser.Core.Merging;

public sealed class MergeWeightCalculator(
    ISourceTrustService? sourceTrustService = null,
    IAttributeStabilityService? attributeStabilityService = null,
    ISourceDisagreementService? sourceDisagreementService = null,
    ICategoryAttributeNormaliserRegistry? categoryAttributeNormaliserRegistry = null)
{
    private readonly ICategoryAttributeNormaliserRegistry categoryAttributeNormaliserRegistry = categoryAttributeNormaliserRegistry ?? new CategoryAttributeNormaliserRegistry([
        new TvAttributeNormaliser(),
        new MonitorAttributeNormaliser(),
        new LaptopAttributeNormaliser(),
        new RefrigeratorAttributeNormaliser()
    ]);

    public decimal CalculateSourceQuality(SourceProduct sourceProduct)
    {
        ArgumentNullException.ThrowIfNull(sourceProduct);

        var normalisedAttributes = sourceProduct.NormalisedAttributes.Values.ToArray();
        var averageConfidence = normalisedAttributes.Length == 0
            ? 0.70m
            : normalisedAttributes.Average(attribute => attribute.Confidence);

        var completeness = CalculateCompleteness(sourceProduct);
        return Clamp(decimal.Round(averageConfidence * 0.70m + completeness * 0.30m, 4, MidpointRounding.AwayFromZero));
    }

    public decimal CalculateAttributeReliability(NormalisedAttributeValue attribute, CanonicalAttributeValue? existingAttribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);

        if (existingAttribute is null)
        {
            return Clamp(decimal.Round(attribute.Confidence, 4, MidpointRounding.AwayFromZero));
        }

        var evidenceConfidence = existingAttribute.Evidence.Count == 0
            ? existingAttribute.Confidence
            : existingAttribute.Evidence.Average(evidence => evidence.Confidence);
        var agreementFactor = existingAttribute.HasConflict ? 0.55m : 0.95m;
        var continuityFactor = existingAttribute.Evidence.Count >= 2 ? 1.00m : 0.90m;

        return Clamp(decimal.Round(
            evidenceConfidence * 0.55m
            + attribute.Confidence * 0.25m
            + agreementFactor * 0.15m
            + continuityFactor * 0.05m,
            4,
            MidpointRounding.AwayFromZero));
    }

    public decimal CalculateRecencyFactor(DateTime observedUtc, DateTime referenceUtc)
    {
        if (observedUtc == default || referenceUtc == default)
        {
            return 0.85m;
        }

        if (observedUtc >= referenceUtc)
        {
            return 1.00m;
        }

        var ageDays = Math.Max(0m, (decimal)(referenceUtc - observedUtc).TotalDays);
        return ageDays switch
        {
            <= 1m => 1.00m,
            <= 7m => 0.96m,
            <= 30m => 0.88m,
            <= 90m => 0.76m,
            _ => 0.65m
        };
    }

    public decimal CalculateIncomingWeight(SourceProduct sourceProduct, NormalisedAttributeValue attribute, CanonicalAttributeValue? existingAttribute)
    {
        var referenceUtc = GetReferenceUtc(sourceProduct, existingAttribute);
        var historicalTrust = GetHistoricalTrust(sourceProduct.SourceName, sourceProduct.CategoryKey);
        var stabilityScore = GetAttributeStability(sourceProduct.CategoryKey, attribute.AttributeKey);
        var disagreementAdjustment = GetSourceDisagreementAdjustment(sourceProduct.SourceName, sourceProduct.CategoryKey, attribute.AttributeKey);

        return Clamp(decimal.Round(
            historicalTrust
            * CalculateSourceQuality(sourceProduct)
            * CalculateAttributeReliability(attribute, existingAttribute)
            * stabilityScore
            * disagreementAdjustment
            * CalculateRecencyFactor(sourceProduct.FetchedUtc, referenceUtc),
            4,
            MidpointRounding.AwayFromZero));
    }

    public decimal CalculateExistingWeight(CanonicalAttributeValue attribute, DateTime referenceUtc)
    {
        ArgumentNullException.ThrowIfNull(attribute);

        if (attribute.MergeWeight > 0m)
        {
            return Clamp(attribute.MergeWeight);
        }

        var observedUtc = attribute.LastObservedUtc == default
            ? referenceUtc
            : attribute.LastObservedUtc;
        var sourceQuality = attribute.SourceQualityScore > 0m ? attribute.SourceQualityScore : attribute.Confidence;
        var historicalTrust = attribute.HistoricalTrustScore > 0m ? attribute.HistoricalTrustScore : 0.72m;
        var reliability = attribute.ReliabilityScore > 0m ? attribute.ReliabilityScore : attribute.Confidence;
        var stability = attribute.StabilityScore > 0m ? attribute.StabilityScore : 0.90m;

        return Clamp(decimal.Round(
            historicalTrust
            * sourceQuality
            * reliability
            * stability
            * CalculateRecencyFactor(observedUtc, referenceUtc),
            4,
            MidpointRounding.AwayFromZero));
    }

    public decimal GetHistoricalTrust(string sourceName, string categoryKey)
    {
        if (sourceTrustService is null || string.IsNullOrWhiteSpace(sourceName) || string.IsNullOrWhiteSpace(categoryKey))
        {
            return 0.72m;
        }

        return Clamp(sourceTrustService.GetHistoricalTrustScore(sourceName, categoryKey));
    }

    public decimal GetAttributeStability(string categoryKey, string attributeKey)
    {
        if (attributeStabilityService is null || string.IsNullOrWhiteSpace(categoryKey) || string.IsNullOrWhiteSpace(attributeKey))
        {
            return 0.90m;
        }

        return Clamp(attributeStabilityService.GetStabilityScore(categoryKey, attributeKey));
    }

    public decimal GetSourceDisagreementAdjustment(string sourceName, string categoryKey, string attributeKey)
    {
        if (sourceDisagreementService is null || string.IsNullOrWhiteSpace(sourceName) || string.IsNullOrWhiteSpace(categoryKey) || string.IsNullOrWhiteSpace(attributeKey))
        {
            return 1.00m;
        }

        return Clamp(sourceDisagreementService.GetSourceAttributeAdjustment(sourceName, categoryKey, attributeKey));
    }

    private static DateTime GetReferenceUtc(SourceProduct sourceProduct, CanonicalAttributeValue? existingAttribute)
    {
        if (sourceProduct.FetchedUtc == default)
        {
            return existingAttribute?.LastObservedUtc == default
                ? DateTime.UtcNow
                : existingAttribute!.LastObservedUtc;
        }

        if (existingAttribute is null || existingAttribute.LastObservedUtc == default)
        {
            return sourceProduct.FetchedUtc;
        }

        return sourceProduct.FetchedUtc >= existingAttribute.LastObservedUtc
            ? sourceProduct.FetchedUtc
            : existingAttribute.LastObservedUtc;
    }

    private decimal CalculateCompleteness(SourceProduct sourceProduct)
    {
        var keysToCheck = categoryAttributeNormaliserRegistry
            .GetCompletenessAttributeKeys(sourceProduct.CategoryKey)
            .Concat(categoryAttributeNormaliserRegistry.GetIdentityAttributeKeys(sourceProduct.CategoryKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (keysToCheck.Length == 0)
        {
            var mappedCount = sourceProduct.NormalisedAttributes.Values.Count(attribute => attribute.Value is not null);
            var fallbackIdentityCount = 0;
            if (!string.IsNullOrWhiteSpace(sourceProduct.Brand))
            {
                fallbackIdentityCount += 1;
            }

            if (!string.IsNullOrWhiteSpace(sourceProduct.ModelNumber))
            {
                fallbackIdentityCount += 1;
            }

            if (!string.IsNullOrWhiteSpace(sourceProduct.Gtin))
            {
                fallbackIdentityCount += 1;
            }

            return Math.Min(1.00m, (mappedCount + fallbackIdentityCount) / 8.00m);
        }

        var populatedCount = keysToCheck.Count(key => HasSourceValue(sourceProduct, key));
        return Math.Min(1.00m, decimal.Divide(populatedCount, keysToCheck.Length));
    }

    private static bool HasSourceValue(SourceProduct sourceProduct, string key)
    {
        return key switch
        {
            "brand" => !string.IsNullOrWhiteSpace(sourceProduct.Brand),
            "model_number" => !string.IsNullOrWhiteSpace(sourceProduct.ModelNumber),
            "gtin" => !string.IsNullOrWhiteSpace(sourceProduct.Gtin),
            _ => sourceProduct.NormalisedAttributes.TryGetValue(key, out var attribute)
                && attribute.Value is not null
                && (attribute.Value is not string text || !string.IsNullOrWhiteSpace(text))
        };
    }

    private static decimal Clamp(decimal value)
    {
        return Math.Min(1.00m, Math.Max(0.05m, value));
    }
}