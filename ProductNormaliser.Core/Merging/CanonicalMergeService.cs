using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Merging;

public sealed class CanonicalMergeService(
    ConfidenceScorer? confidenceScorer = null,
    MergeWeightCalculator? mergeWeightCalculator = null) : ICanonicalMergeService
{
    private readonly ConfidenceScorer confidenceScorer = confidenceScorer ?? new ConfidenceScorer();
    private readonly MergeWeightCalculator mergeWeightCalculator = mergeWeightCalculator ?? new MergeWeightCalculator();

    public CanonicalProduct Merge(CanonicalProduct? existing, SourceProduct incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);

        var canonical = existing ?? CreateCanonicalSkeleton(incoming);

        canonical.CategoryKey = string.IsNullOrWhiteSpace(canonical.CategoryKey) ? incoming.CategoryKey : canonical.CategoryKey;
        canonical.Brand = ChoosePreferredString(canonical.Brand, incoming.Brand, canonical.Brand.Length == 0);
        canonical.ModelNumber = ChoosePreferredString(canonical.ModelNumber, incoming.ModelNumber, string.IsNullOrWhiteSpace(canonical.ModelNumber));
        canonical.Gtin = ChoosePreferredString(canonical.Gtin, incoming.Gtin, string.IsNullOrWhiteSpace(canonical.Gtin));
        canonical.DisplayName = ChoosePreferredString(canonical.DisplayName, incoming.Title, string.IsNullOrWhiteSpace(canonical.DisplayName));

        MergeSourceLink(canonical, incoming);
        MergeOfferIds(canonical, incoming);
        MergeAttributes(canonical, incoming);

        canonical.UpdatedUtc = incoming.FetchedUtc == default ? DateTime.UtcNow : incoming.FetchedUtc;

        return canonical;
    }

    private CanonicalProduct CreateCanonicalSkeleton(SourceProduct incoming)
    {
        var createdUtc = incoming.FetchedUtc == default ? DateTime.UtcNow : incoming.FetchedUtc;
        return new CanonicalProduct
        {
            Id = incoming.Gtin?.Trim()
                ?? $"{incoming.Brand?.Trim() ?? "unknown"}:{incoming.ModelNumber?.Trim() ?? incoming.Id}",
            CategoryKey = incoming.CategoryKey,
            Brand = incoming.Brand ?? string.Empty,
            ModelNumber = incoming.ModelNumber,
            Gtin = incoming.Gtin,
            DisplayName = incoming.Title ?? incoming.ModelNumber ?? incoming.Id,
            CreatedUtc = createdUtc,
            UpdatedUtc = createdUtc
        };
    }

    private void MergeAttributes(CanonicalProduct canonical, SourceProduct incoming)
    {
        foreach (var incomingAttribute in incoming.NormalisedAttributes.Values)
        {
            var evidence = BuildEvidence(incoming, incomingAttribute);

            if (!canonical.Attributes.TryGetValue(incomingAttribute.AttributeKey, out var canonicalAttribute))
            {
                var initialIncomingWeight = mergeWeightCalculator.CalculateIncomingWeight(incoming, incomingAttribute, null);
                var sourceQuality = mergeWeightCalculator.CalculateSourceQuality(incoming);
                var reliability = mergeWeightCalculator.CalculateAttributeReliability(incomingAttribute, null);

                canonical.Attributes[incomingAttribute.AttributeKey] = new CanonicalAttributeValue
                {
                    AttributeKey = incomingAttribute.AttributeKey,
                    Value = incomingAttribute.Value,
                    ValueType = incomingAttribute.ValueType,
                    Unit = incomingAttribute.Unit,
                    Confidence = confidenceScorer.ScoreAttributeCandidate(incomingAttribute, incoming),
                    HasConflict = false,
                    MergeWeight = initialIncomingWeight,
                    ReliabilityScore = reliability,
                    SourceQualityScore = sourceQuality,
                    WinningSourceName = incoming.SourceName,
                    LastObservedUtc = incoming.FetchedUtc,
                    Evidence = [evidence]
                };

                continue;
            }

            UpsertEvidence(canonicalAttribute, evidence);

            var valuesAgree = AreEquivalent(canonicalAttribute, incomingAttribute);
            if (valuesAgree)
            {
                canonicalAttribute.MergeWeight = Math.Max(
                    canonicalAttribute.MergeWeight,
                    mergeWeightCalculator.CalculateIncomingWeight(incoming, incomingAttribute, canonicalAttribute));
                canonicalAttribute.ReliabilityScore = Math.Max(
                    canonicalAttribute.ReliabilityScore,
                    mergeWeightCalculator.CalculateAttributeReliability(incomingAttribute, canonicalAttribute));
                canonicalAttribute.SourceQualityScore = Math.Max(
                    canonicalAttribute.SourceQualityScore,
                    mergeWeightCalculator.CalculateSourceQuality(incoming));
                canonicalAttribute.LastObservedUtc = incoming.FetchedUtc;

                canonicalAttribute.Confidence = confidenceScorer.ScoreMergedAttribute(
                    Math.Max(canonicalAttribute.Confidence, confidenceScorer.ScoreAttributeCandidate(incomingAttribute, incoming)),
                    canonicalAttribute.Evidence.Count,
                    exactAgreement: true,
                    hasConflict: false);

                continue;
            }

            var incomingScore = confidenceScorer.ScoreAttributeCandidate(incomingAttribute, incoming);
            var incomingWeight = mergeWeightCalculator.CalculateIncomingWeight(incoming, incomingAttribute, canonicalAttribute);
            var existingWeight = mergeWeightCalculator.CalculateExistingWeight(canonicalAttribute, incoming.FetchedUtc == default ? DateTime.UtcNow : incoming.FetchedUtc);
            var shouldReplace = incomingWeight > existingWeight
                || canonicalAttribute.Value is null;

            if (shouldReplace)
            {
                canonicalAttribute.Value = incomingAttribute.Value;
                canonicalAttribute.ValueType = incomingAttribute.ValueType;
                canonicalAttribute.Unit = incomingAttribute.Unit;
                canonicalAttribute.WinningSourceName = incoming.SourceName;
                canonicalAttribute.LastObservedUtc = incoming.FetchedUtc;
                canonicalAttribute.SourceQualityScore = mergeWeightCalculator.CalculateSourceQuality(incoming);
            }

            canonicalAttribute.HasConflict = true;
            canonicalAttribute.MergeWeight = shouldReplace ? incomingWeight : Math.Max(existingWeight, incomingWeight);
            canonicalAttribute.ReliabilityScore = mergeWeightCalculator.CalculateAttributeReliability(incomingAttribute, canonicalAttribute);
            if (canonicalAttribute.LastObservedUtc == default)
            {
                canonicalAttribute.LastObservedUtc = incoming.FetchedUtc;
            }

            canonicalAttribute.Confidence = confidenceScorer.ScoreMergedAttribute(
                Math.Max(canonicalAttribute.Confidence, incomingScore),
                canonicalAttribute.Evidence.Count,
                exactAgreement: false,
                hasConflict: true);
        }
    }

    private static void MergeSourceLink(CanonicalProduct canonical, SourceProduct incoming)
    {
        var existingLink = canonical.Sources.FirstOrDefault(source =>
            source.SourceProductId == incoming.Id
            && source.SourceName == incoming.SourceName
            && source.SourceUrl == incoming.SourceUrl);

        if (existingLink is null)
        {
            canonical.Sources.Add(new ProductSourceLink
            {
                SourceName = incoming.SourceName,
                SourceProductId = incoming.Id,
                SourceUrl = incoming.SourceUrl,
                FirstSeenUtc = incoming.FetchedUtc,
                LastSeenUtc = incoming.FetchedUtc
            });

            return;
        }

        existingLink.LastSeenUtc = incoming.FetchedUtc;
    }

    private static void MergeOfferIds(CanonicalProduct canonical, SourceProduct incoming)
    {
        foreach (var offerId in incoming.Offers.Select(offer => offer.Id).Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            if (!canonical.OfferIds.Contains(offerId, StringComparer.Ordinal))
            {
                canonical.OfferIds.Add(offerId);
            }
        }
    }

    private static AttributeEvidence BuildEvidence(SourceProduct incoming, NormalisedAttributeValue incomingAttribute)
    {
        return new AttributeEvidence
        {
            SourceName = incoming.SourceName,
            SourceUrl = incoming.SourceUrl,
            SourceProductId = incoming.Id,
            SourceAttributeKey = incomingAttribute.SourceAttributeKey ?? incomingAttribute.AttributeKey,
            RawValue = incomingAttribute.OriginalValue,
            SelectorOrPath = incomingAttribute.ParseNotes,
            Confidence = incomingAttribute.Confidence,
            ObservedUtc = incoming.FetchedUtc
        };
    }

    private static void UpsertEvidence(CanonicalAttributeValue canonicalAttribute, AttributeEvidence evidence)
    {
        var existingEvidence = canonicalAttribute.Evidence.FirstOrDefault(item =>
            item.SourceName == evidence.SourceName
            && item.SourceUrl == evidence.SourceUrl
            && item.SourceProductId == evidence.SourceProductId
            && item.SourceAttributeKey == evidence.SourceAttributeKey);

        if (existingEvidence is null)
        {
            canonicalAttribute.Evidence.Add(evidence);
            return;
        }

        existingEvidence.RawValue = evidence.RawValue;
        existingEvidence.SelectorOrPath = evidence.SelectorOrPath;
        existingEvidence.Confidence = evidence.Confidence;
        existingEvidence.ObservedUtc = evidence.ObservedUtc;
    }

    private static bool AreEquivalent(CanonicalAttributeValue canonicalAttribute, NormalisedAttributeValue incomingAttribute)
    {
        if (canonicalAttribute.Value is null || incomingAttribute.Value is null)
        {
            return canonicalAttribute.Value is null && incomingAttribute.Value is null;
        }

        if (IsNumeric(canonicalAttribute.Value) && IsNumeric(incomingAttribute.Value))
        {
            var left = Convert.ToDecimal(canonicalAttribute.Value);
            var right = Convert.ToDecimal(incomingAttribute.Value);
            var tolerance = Math.Max(0.5m, Math.Abs(left) * 0.02m);
            return Math.Abs(left - right) <= tolerance;
        }

        return string.Equals(
            canonicalAttribute.Value.ToString()?.Trim(),
            incomingAttribute.Value.ToString()?.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNumeric(object value)
    {
        return value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
    }

    private static string ChoosePreferredString(string? currentValue, string? incomingValue, bool preferIncoming)
    {
        if (preferIncoming && !string.IsNullOrWhiteSpace(incomingValue))
        {
            return incomingValue.Trim();
        }

        return currentValue?.Trim() ?? string.Empty;
    }
}