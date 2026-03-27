using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Normalisation;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Core.Merging;

public sealed class ProductIdentityResolver(
    ProductFingerprintBuilder? fingerprintBuilder = null,
    ConfidenceScorer? confidenceScorer = null,
    ICategoryAttributeNormaliserRegistry? categoryAttributeNormaliserRegistry = null) : IProductIdentityResolver
{
    private const string SmartphoneCategoryKey = "smartphone";
    private static readonly IReadOnlyDictionary<string, string[]> StrongDisambiguatorKeysByCategory = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        [SmartphoneCategoryKey] = ["storage_capacity_gb", "manufacturer_part_number", "regional_variant", "carrier_lock_status"],
        ["tablet"] = ["connectivity", "cellular_generation", "manufacturer_part_number", "regional_variant", "storage_capacity_gb"],
        ["headphones"] = ["connection_type", "manufacturer_part_number"],
        ["speakers"] = ["connection_type", "speaker_type", "manufacturer_part_number"]
    };

    private readonly ProductFingerprintBuilder fingerprintBuilder = fingerprintBuilder ?? new ProductFingerprintBuilder();
    private readonly ConfidenceScorer confidenceScorer = confidenceScorer ?? new ConfidenceScorer();
    private readonly ICategoryAttributeNormaliserRegistry categoryAttributeNormaliserRegistry = categoryAttributeNormaliserRegistry ?? DefaultCategoryRegistries.CreateAttributeNormaliserRegistry();

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

        var manufacturerPartNumberMatch = FindManufacturerPartNumberMatch(sourceProduct, candidates);
        if (manufacturerPartNumberMatch is not null)
        {
            return manufacturerPartNumberMatch;
        }

        var broaderCandidates = FilterCandidatesForBroaderMatching(sourceProduct, candidates);

        var brandModelMatch = broaderCandidates.FirstOrDefault(candidate =>
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

        var categoryIdentityMatch = FindCategoryIdentityMatch(sourceProduct, broaderCandidates);
        if (categoryIdentityMatch is not null)
        {
            return categoryIdentityMatch;
        }

        var bestSimilarityMatch = broaderCandidates
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

        var strongConflictReason = GetStrongVariantConflictReason(sourceProduct, candidates);
        if (strongConflictReason is not null)
        {
            return new ProductIdentityMatchResult
            {
                IsMatch = false,
                Confidence = 0.00m,
                MatchReason = strongConflictReason
            };
        }

        return new ProductIdentityMatchResult
        {
            IsMatch = false,
            Confidence = 0.00m,
            MatchReason = "No sufficiently strong product identity match found."
        };
    }

    private ProductIdentityMatchResult? FindManufacturerPartNumberMatch(SourceProduct sourceProduct, IReadOnlyCollection<CanonicalProduct> candidates)
    {
        if (!TryGetStrongDisambiguatorKeys(sourceProduct.CategoryKey, out _)
            || !TryGetComparableValue(sourceProduct, "manufacturer_part_number", out var sourceManufacturerPartNumber))
        {
            return null;
        }

        var match = candidates.FirstOrDefault(candidate =>
            string.Equals(candidate.CategoryKey, sourceProduct.CategoryKey, StringComparison.OrdinalIgnoreCase)
            && TryGetComparableValue(candidate, "manufacturer_part_number", out var candidateManufacturerPartNumber)
            && string.Equals(candidateManufacturerPartNumber, sourceManufacturerPartNumber, StringComparison.OrdinalIgnoreCase)
            && PrimaryIdentitySignalsAreCompatible(sourceProduct, candidate));

        return match is null
            ? null
            : new ProductIdentityMatchResult
            {
                CanonicalProductId = match.Id,
                IsMatch = true,
                Confidence = 0.98m,
                MatchReason = "Exact manufacturer part number match."
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

    private IReadOnlyCollection<CanonicalProduct> FilterCandidatesForBroaderMatching(SourceProduct sourceProduct, IReadOnlyCollection<CanonicalProduct> candidates)
    {
        if (!TryGetStrongDisambiguatorKeys(sourceProduct.CategoryKey, out _))
        {
            return candidates;
        }

        return candidates
            .Where(candidate => !HasStrongVariantConflict(sourceProduct, candidate))
            .ToArray();
    }

    private static bool PrimaryIdentitySignalsAreCompatible(SourceProduct sourceProduct, CanonicalProduct candidate)
    {
        return !HasComparableConflict(sourceProduct, candidate, "brand")
            && !HasComparableConflict(sourceProduct, candidate, "model_number");
    }

    private static bool HasStrongVariantConflict(SourceProduct sourceProduct, CanonicalProduct candidate)
    {
        if (!TryGetStrongDisambiguatorKeys(sourceProduct.CategoryKey, out var disambiguatorKeys)
            || !string.Equals(candidate.CategoryKey, sourceProduct.CategoryKey, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return disambiguatorKeys.Any(key => HasComparableConflict(sourceProduct, candidate, key));
    }

    private static string? GetStrongVariantConflictReason(SourceProduct sourceProduct, IReadOnlyCollection<CanonicalProduct> candidates)
    {
        if (!TryGetStrongDisambiguatorKeys(sourceProduct.CategoryKey, out _)
            || !candidates.Any(candidate => HasStrongVariantConflict(sourceProduct, candidate)))
        {
            return null;
        }

        return sourceProduct.CategoryKey.ToLowerInvariant() switch
        {
            SmartphoneCategoryKey => "Strong smartphone variant conflict prevented a safe match.",
            "tablet" => "Strong tablet variant conflict prevented a safe match.",
            "headphones" => "Strong headphones variant conflict prevented a safe match.",
            "speakers" => "Strong speakers variant conflict prevented a safe match.",
            _ => "Strong category variant conflict prevented a safe match."
        };
    }

    private static bool HasComparableConflict(SourceProduct sourceProduct, CanonicalProduct candidate, string key)
    {
        return TryGetComparableValue(sourceProduct, key, out var sourceValue)
            && TryGetComparableValue(candidate, key, out var candidateValue)
            && !string.Equals(sourceValue, candidateValue, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSmartphoneCategory(string categoryKey)
    {
        return string.Equals(categoryKey, SmartphoneCategoryKey, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetStrongDisambiguatorKeys(string categoryKey, out string[] keys)
    {
        return StrongDisambiguatorKeysByCategory.TryGetValue(categoryKey, out keys!);
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