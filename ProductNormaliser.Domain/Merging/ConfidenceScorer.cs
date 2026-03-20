using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Merging;

public sealed class ConfidenceScorer
{
    public decimal ScoreIdentityByGtin() => 1.00m;

    public decimal ScoreIdentityByBrandModel() => 0.97m;

    public decimal ScoreIdentityBySimilarity(decimal similarity)
    {
        return Clamp(0.60m + (similarity * 0.35m));
    }

    public decimal ScoreAttributeCandidate(NormalisedAttributeValue value, SourceProduct sourceProduct)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(sourceProduct);

        var score = value.Confidence + GetSourceTrustBonus(sourceProduct, sourceProduct.Brand);
        return Clamp(score);
    }

    public decimal ScoreMergedAttribute(decimal baseConfidence, int supportingSourceCount, bool exactAgreement, bool hasConflict)
    {
        var agreementBonus = exactAgreement ? 0.05m : 0.00m;
        var supportBonus = Math.Min(0.14m, Math.Max(0, supportingSourceCount - 1) * 0.07m);
        var conflictPenalty = hasConflict ? 0.20m : 0.00m;

        return Clamp(baseConfidence + agreementBonus + supportBonus - conflictPenalty);
    }

    public decimal GetSourceTrustBonus(SourceProduct sourceProduct, string? brand)
    {
        ArgumentNullException.ThrowIfNull(sourceProduct);

        var sourceName = sourceProduct.SourceName.Trim().ToLowerInvariant();
        var normalisedBrand = string.IsNullOrWhiteSpace(brand)
            ? string.Empty
            : brand.Trim().ToLowerInvariant();

        if (normalisedBrand.Length > 0 && sourceName.Contains(normalisedBrand, StringComparison.Ordinal))
        {
            return 0.12m;
        }

        if (sourceName.Contains("manufacturer", StringComparison.Ordinal))
        {
            return 0.10m;
        }

        return 0.00m;
    }

    private static decimal Clamp(decimal value)
    {
        return decimal.Clamp(decimal.Round(value, 4, MidpointRounding.AwayFromZero), 0.05m, 0.99m);
    }
}