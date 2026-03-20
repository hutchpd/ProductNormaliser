using System.Security.Cryptography;
using System.Text;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Mongo.Repositories;

namespace ProductNormaliser.Infrastructure.Crawling;

public sealed class DeltaProcessor(IRawPageStore rawPageStore, ISourceProductStore sourceProductStore) : IDeltaProcessor
{
    public string ComputeHash(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(html));
        return Convert.ToHexString(hashBytes);
    }

    public async Task<DeltaDetectionResult> DetectAsync(string sourceName, string sourceUrl, string html, CancellationToken cancellationToken)
    {
        var contentHash = ComputeHash(html);
        var latestPage = await rawPageStore.GetLatestBySourceAsync(sourceName, sourceUrl, cancellationToken);

        return new DeltaDetectionResult
        {
            ContentHash = contentHash,
            IsUnchanged = latestPage is not null && string.Equals(latestPage.ContentHash, contentHash, StringComparison.Ordinal)
        };
    }

    public async Task<SemanticDeltaResult> DetectSemanticChangesAsync(SourceProduct sourceProduct, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceProduct);

        var existing = await sourceProductStore.GetBySourceAsync(sourceProduct.SourceName, sourceProduct.SourceUrl, cancellationToken);
        if (existing is null)
        {
            return new SemanticDeltaResult
            {
                HasMeaningfulChanges = true,
                HasAttributeChanges = sourceProduct.NormalisedAttributes.Count > 0,
                HasOfferChanges = sourceProduct.Offers.Count > 0,
                PriceChanged = sourceProduct.Offers.Any(offer => offer.Price is not null),
                AvailabilityChanged = sourceProduct.Offers.Any(offer => !string.IsNullOrWhiteSpace(offer.Availability)),
                ChangedAttributeKeys = sourceProduct.NormalisedAttributes.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray(),
                Summary = "First observed product snapshot."
            };
        }

        var changedAttributeKeys = sourceProduct.NormalisedAttributes.Keys
            .Union(existing.NormalisedAttributes.Keys, StringComparer.OrdinalIgnoreCase)
            .Where(key => !ValuesMatch(
                sourceProduct.NormalisedAttributes.TryGetValue(key, out var currentAttribute) ? currentAttribute.Value : null,
                existing.NormalisedAttributes.TryGetValue(key, out var existingAttribute) ? existingAttribute.Value : null))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        var priceChanged = OffersDiffer(sourceProduct.Offers.Select(offer => offer.Price), existing.Offers.Select(offer => offer.Price));
        var availabilityChanged = OffersDiffer(sourceProduct.Offers.Select(offer => offer.Availability), existing.Offers.Select(offer => offer.Availability));
        var hasOfferChanges = priceChanged || availabilityChanged || sourceProduct.Offers.Count != existing.Offers.Count;
        var hasAttributeChanges = changedAttributeKeys.Length > 0;

        return new SemanticDeltaResult
        {
            HasMeaningfulChanges = hasAttributeChanges || hasOfferChanges,
            HasAttributeChanges = hasAttributeChanges,
            HasOfferChanges = hasOfferChanges,
            PriceChanged = priceChanged,
            AvailabilityChanged = availabilityChanged,
            ChangedAttributeKeys = changedAttributeKeys,
            Summary = BuildSummary(hasAttributeChanges, changedAttributeKeys, hasOfferChanges, priceChanged, availabilityChanged)
        };
    }

    private static bool OffersDiffer<T>(IEnumerable<T> currentValues, IEnumerable<T> existingValues)
    {
        return !currentValues.SequenceEqual(existingValues);
    }

    private static bool ValuesMatch(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return string.Equals(left.ToString()?.Trim(), right.ToString()?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSummary(bool hasAttributeChanges, IReadOnlyList<string> changedAttributeKeys, bool hasOfferChanges, bool priceChanged, bool availabilityChanged)
    {
        if (!hasAttributeChanges && !hasOfferChanges)
        {
            return "No semantic changes detected.";
        }

        var parts = new List<string>();
        if (hasAttributeChanges)
        {
            parts.Add($"Spec changes: {string.Join(", ", changedAttributeKeys)}");
        }

        if (hasOfferChanges)
        {
            if (priceChanged && availabilityChanged)
            {
                parts.Add("Offer changes: price and availability");
            }
            else if (priceChanged)
            {
                parts.Add("Offer changes: price");
            }
            else if (availabilityChanged)
            {
                parts.Add("Offer changes: availability");
            }
            else
            {
                parts.Add("Offer changes detected");
            }
        }

        return string.Join("; ", parts);
    }
}