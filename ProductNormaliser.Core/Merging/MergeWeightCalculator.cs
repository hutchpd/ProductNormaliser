using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Merging;

public sealed class MergeWeightCalculator
{
    public decimal CalculateSourceQuality(SourceProduct sourceProduct)
    {
        ArgumentNullException.ThrowIfNull(sourceProduct);

        var normalisedAttributes = sourceProduct.NormalisedAttributes.Values.ToArray();
        var averageConfidence = normalisedAttributes.Length == 0
            ? 0.70m
            : normalisedAttributes.Average(attribute => attribute.Confidence);

        var mappedCount = normalisedAttributes.Count(attribute => attribute.Value is not null);
        var identityCount = 0;
        if (!string.IsNullOrWhiteSpace(sourceProduct.Brand))
        {
            identityCount += 1;
        }

        if (!string.IsNullOrWhiteSpace(sourceProduct.ModelNumber))
        {
            identityCount += 1;
        }

        if (!string.IsNullOrWhiteSpace(sourceProduct.Gtin))
        {
            identityCount += 1;
        }

        var completeness = Math.Min(1.00m, (mappedCount + identityCount) / 8.00m);
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
        return Clamp(decimal.Round(
            CalculateSourceQuality(sourceProduct)
            * CalculateAttributeReliability(attribute, existingAttribute)
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
        var reliability = attribute.ReliabilityScore > 0m ? attribute.ReliabilityScore : attribute.Confidence;

        return Clamp(decimal.Round(
            sourceQuality
            * reliability
            * CalculateRecencyFactor(observedUtc, referenceUtc),
            4,
            MidpointRounding.AwayFromZero));
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

    private static decimal Clamp(decimal value)
    {
        return Math.Min(1.00m, Math.Max(0.05m, value));
    }
}