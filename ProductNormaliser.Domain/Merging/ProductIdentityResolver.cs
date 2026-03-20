using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Normalisation;

namespace ProductNormaliser.Core.Merging;

public sealed class ProductIdentityResolver(
    ProductFingerprintBuilder? fingerprintBuilder = null,
    ConfidenceScorer? confidenceScorer = null,
    ICategoryAttributeNormaliserRegistry? categoryAttributeNormaliserRegistry = null) : IProductIdentityResolver
{
    private readonly ProductFingerprintBuilder fingerprintBuilder = fingerprintBuilder ?? new ProductFingerprintBuilder();
    private readonly ConfidenceScorer confidenceScorer = confidenceScorer ?? new ConfidenceScorer();
    private readonly ICategoryAttributeNormaliserRegistry categoryAttributeNormaliserRegistry = categoryAttributeNormaliserRegistry ?? new CategoryAttributeNormaliserRegistry([
        new TvAttributeNormaliser(),
        new MonitorAttributeNormaliser(),
        new LaptopAttributeNormaliser(),
        new RefrigeratorAttributeNormaliser()
    ]);

    public ProductIdentityMatchResult Match(SourceProduct sourceProduct, IReadOnlyCollection<CanonicalProduct> candidates)
    {
        ArgumentNullException.ThrowIfNull(sourceProduct);
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Count == 0)
        {
            return new ProductIdentityMatchResult
            {
                IsMatch = false,
                Confidence = 0.00m,
                MatchReason = "No canonical candidates available."
            };
        }

        var sourceFingerprint = fingerprintBuilder.Build(sourceProduct);

        var gtinMatch = candidates.FirstOrDefault(candidate =>
            !string.IsNullOrWhiteSpace(sourceProduct.Gtin)
            && string.Equals(candidate.Gtin, sourceProduct.Gtin, StringComparison.OrdinalIgnoreCase));

        if (gtinMatch is not null)
        {
            return new ProductIdentityMatchResult
            {
                CanonicalProductId = gtinMatch.Id,
                IsMatch = true,
                Confidence = confidenceScorer.ScoreIdentityByGtin(),
                MatchReason = "Exact GTIN match."
            };
        }

        var brandModelMatch = candidates.FirstOrDefault(candidate =>
            string.Equals(candidate.Brand?.Trim(), sourceProduct.Brand?.Trim(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.ModelNumber?.Trim(), sourceProduct.ModelNumber?.Trim(), StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(candidate.Brand)
            && !string.IsNullOrWhiteSpace(candidate.ModelNumber));

        if (brandModelMatch is not null)
        {
            return new ProductIdentityMatchResult
            {
                CanonicalProductId = brandModelMatch.Id,
                IsMatch = true,
                Confidence = confidenceScorer.ScoreIdentityByBrandModel(),
                MatchReason = "Exact brand and model number match."
            };
        }

        var categoryIdentityMatch = FindCategoryIdentityMatch(sourceProduct, candidates);
        if (categoryIdentityMatch is not null)
        {
            return categoryIdentityMatch;
        }

        var bestSimilarityMatch = candidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Similarity = fingerprintBuilder.CalculateSimilarity(sourceFingerprint, fingerprintBuilder.Build(candidate))
            })
            .OrderByDescending(result => result.Similarity)
            .FirstOrDefault();

        if (bestSimilarityMatch is not null && bestSimilarityMatch.Similarity >= 0.86m)
        {
            return new ProductIdentityMatchResult
            {
                CanonicalProductId = bestSimilarityMatch.Candidate.Id,
                IsMatch = true,
                Confidence = confidenceScorer.ScoreIdentityBySimilarity(bestSimilarityMatch.Similarity),
                MatchReason = $"Strong title/model similarity ({bestSimilarityMatch.Similarity:0.00})."
            };
        }

        return new ProductIdentityMatchResult
        {
            IsMatch = false,
            Confidence = 0.00m,
            MatchReason = "No sufficiently strong product identity match found."
        };
    }

    private ProductIdentityMatchResult? FindCategoryIdentityMatch(SourceProduct sourceProduct, IReadOnlyCollection<CanonicalProduct> candidates)
    {
        var identityKeys = categoryAttributeNormaliserRegistry
            .GetIdentityAttributeKeys(sourceProduct.CategoryKey)
            .Where(key => !string.Equals(key, "gtin", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(key, "brand", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(key, "model_number", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (identityKeys.Length == 0 || sourceProduct.NormalisedAttributes.Count == 0)
        {
            return null;
        }

        var match = candidates
            .Where(candidate => string.Equals(candidate.CategoryKey, sourceProduct.CategoryKey, StringComparison.OrdinalIgnoreCase))
            .Select(candidate => new
            {
                Candidate = candidate,
                MatchCount = CountMatchingIdentityAttributes(sourceProduct, candidate, identityKeys),
                ComparedCount = CountComparableIdentityAttributes(sourceProduct, candidate, identityKeys)
            })
            .Where(result => result.MatchCount >= 2 && result.MatchCount == result.ComparedCount)
            .OrderByDescending(result => result.MatchCount)
            .FirstOrDefault();

        if (match is null)
        {
            return null;
        }

        return new ProductIdentityMatchResult
        {
            CanonicalProductId = match.Candidate.Id,
            IsMatch = true,
            Confidence = 0.93m,
            MatchReason = $"Exact match across {match.MatchCount} category identity attributes."
        };
    }

    private static int CountMatchingIdentityAttributes(SourceProduct sourceProduct, CanonicalProduct candidate, IEnumerable<string> identityKeys)
    {
        return identityKeys.Count(key => TryGetComparableValue(sourceProduct, key, out var sourceValue)
            && TryGetComparableValue(candidate, key, out var candidateValue)
            && string.Equals(sourceValue, candidateValue, StringComparison.OrdinalIgnoreCase));
    }

    private static int CountComparableIdentityAttributes(SourceProduct sourceProduct, CanonicalProduct candidate, IEnumerable<string> identityKeys)
    {
        return identityKeys.Count(key => TryGetComparableValue(sourceProduct, key, out _)
            && TryGetComparableValue(candidate, key, out _));
    }

    private static bool TryGetComparableValue(SourceProduct product, string key, out string value)
    {
        value = key switch
        {
            "brand" => product.Brand?.Trim() ?? string.Empty,
            "model_number" => product.ModelNumber?.Trim() ?? string.Empty,
            "gtin" => product.Gtin?.Trim() ?? string.Empty,
            _ => product.NormalisedAttributes.TryGetValue(key, out var attribute)
                ? ConvertToComparableString(attribute.Value)
                : string.Empty
        };

        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetComparableValue(CanonicalProduct product, string key, out string value)
    {
        value = key switch
        {
            "brand" => product.Brand?.Trim() ?? string.Empty,
            "model_number" => product.ModelNumber?.Trim() ?? string.Empty,
            "gtin" => product.Gtin?.Trim() ?? string.Empty,
            _ => product.Attributes.TryGetValue(key, out var attribute)
                ? ConvertToComparableString(attribute.Value)
                : string.Empty
        };

        return !string.IsNullOrWhiteSpace(value);
    }

    private static string ConvertToComparableString(object? value)
    {
        return value switch
        {
            null => string.Empty,
            decimal decimalValue => decimalValue.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture),
            double doubleValue => doubleValue.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture),
            float floatValue => floatValue.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString()?.Trim() ?? string.Empty
        };
    }
}