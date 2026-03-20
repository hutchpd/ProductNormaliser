using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Merging;

public sealed class ProductIdentityResolver(
    ProductFingerprintBuilder? fingerprintBuilder = null,
    ConfidenceScorer? confidenceScorer = null) : IProductIdentityResolver
{
    private readonly ProductFingerprintBuilder fingerprintBuilder = fingerprintBuilder ?? new ProductFingerprintBuilder();
    private readonly ConfidenceScorer confidenceScorer = confidenceScorer ?? new ConfidenceScorer();

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
}