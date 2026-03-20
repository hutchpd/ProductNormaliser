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
            var initialDetails = sourceProduct.NormalisedAttributes.Values
                .Select(attribute => new SemanticChangeDetail
                {
                    AttributeKey = attribute.AttributeKey,
                    OldValue = null,
                    NewValue = attribute.Value,
                    ChangeType = "attribute"
                })
                .Concat(sourceProduct.Offers.SelectMany(offer => BuildOfferDetails(null, offer)))
                .ToArray();

            return new SemanticDeltaResult
            {
                HasMeaningfulChanges = true,
                HasAttributeChanges = sourceProduct.NormalisedAttributes.Count > 0,
                HasOfferChanges = sourceProduct.Offers.Count > 0,
                PriceChanged = sourceProduct.Offers.Any(offer => offer.Price is not null),
                AvailabilityChanged = sourceProduct.Offers.Any(offer => !string.IsNullOrWhiteSpace(offer.Availability)),
                ChangedAttributeKeys = sourceProduct.NormalisedAttributes.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray(),
                ChangeDetails = initialDetails,
                Summary = "First observed product snapshot."
            };
        }

        var attributeChanges = sourceProduct.NormalisedAttributes.Keys
            .Union(existing.NormalisedAttributes.Keys, StringComparer.OrdinalIgnoreCase)
            .Select(key => new
            {
                Key = key,
                OldValue = existing.NormalisedAttributes.TryGetValue(key, out var existingAttribute) ? existingAttribute.Value : null,
                NewValue = sourceProduct.NormalisedAttributes.TryGetValue(key, out var currentAttribute) ? currentAttribute.Value : null
            })
            .Where(change => !ValuesMatch(change.NewValue, change.OldValue))
            .OrderBy(change => change.Key, StringComparer.Ordinal)
            .ToArray();

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
        var changeDetails = attributeChanges
            .Select(change => new SemanticChangeDetail
            {
                AttributeKey = change.Key,
                OldValue = change.OldValue,
                NewValue = change.NewValue,
                ChangeType = "attribute"
            })
            .Concat(BuildOfferChangeDetails(existing.Offers, sourceProduct.Offers))
            .ToArray();

        return new SemanticDeltaResult
        {
            HasMeaningfulChanges = hasAttributeChanges || hasOfferChanges,
            HasAttributeChanges = hasAttributeChanges,
            HasOfferChanges = hasOfferChanges,
            PriceChanged = priceChanged,
            AvailabilityChanged = availabilityChanged,
            ChangedAttributeKeys = changedAttributeKeys,
            ChangeDetails = changeDetails,
            Summary = BuildSummary(hasAttributeChanges, changedAttributeKeys, hasOfferChanges, priceChanged, availabilityChanged)
        };
    }

    public IReadOnlyList<ProductChangeEvent> BuildChangeEvents(CanonicalProduct? previousCanonical, CanonicalProduct currentCanonical, SourceProduct sourceProduct, SemanticDeltaResult semanticDelta)
    {
        ArgumentNullException.ThrowIfNull(currentCanonical);
        ArgumentNullException.ThrowIfNull(sourceProduct);
        ArgumentNullException.ThrowIfNull(semanticDelta);

        var timestampUtc = sourceProduct.FetchedUtc == default ? DateTime.UtcNow : sourceProduct.FetchedUtc;
        var changeEvents = new List<ProductChangeEvent>();

        AppendDirectChange(changeEvents, previousCanonical?.Brand, currentCanonical.Brand, "brand", currentCanonical.Id, currentCanonical.CategoryKey, sourceProduct.SourceName, timestampUtc);
        AppendDirectChange(changeEvents, previousCanonical?.ModelNumber, currentCanonical.ModelNumber, "model_number", currentCanonical.Id, currentCanonical.CategoryKey, sourceProduct.SourceName, timestampUtc);
        AppendDirectChange(changeEvents, previousCanonical?.Gtin, currentCanonical.Gtin, "gtin", currentCanonical.Id, currentCanonical.CategoryKey, sourceProduct.SourceName, timestampUtc);

        var attributeKeys = currentCanonical.Attributes.Keys
            .Union(previousCanonical is null ? [] : previousCanonical.Attributes.Keys, StringComparer.OrdinalIgnoreCase)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var key in attributeKeys)
        {
            var oldValue = previousCanonical?.Attributes.TryGetValue(key, out var previousAttribute) == true ? previousAttribute.Value : null;
            var newValue = currentCanonical.Attributes.TryGetValue(key, out var currentAttribute) ? currentAttribute.Value : null;
            AppendDirectChange(changeEvents, oldValue, newValue, key, currentCanonical.Id, currentCanonical.CategoryKey, sourceProduct.SourceName, timestampUtc);
        }

        foreach (var change in semanticDelta.ChangeDetails.Where(change => change.AttributeKey.StartsWith("offer.", StringComparison.OrdinalIgnoreCase)))
        {
            changeEvents.Add(new ProductChangeEvent
            {
                Id = $"change:{currentCanonical.Id}:{change.AttributeKey}:{timestampUtc:yyyyMMddHHmmssfff}:{changeEvents.Count + 1}",
                CanonicalProductId = currentCanonical.Id,
                CategoryKey = currentCanonical.CategoryKey,
                AttributeKey = change.AttributeKey,
                OldValue = change.OldValue,
                NewValue = change.NewValue,
                SourceName = sourceProduct.SourceName,
                TimestampUtc = timestampUtc
            });
        }

        return changeEvents;
    }

    private static IEnumerable<SemanticChangeDetail> BuildOfferChangeDetails(IReadOnlyList<ProductOffer> existingOffers, IReadOnlyList<ProductOffer> currentOffers)
    {
        var details = new List<SemanticChangeDetail>();
        var existingOffer = existingOffers.FirstOrDefault();
        var currentOffer = currentOffers.FirstOrDefault();

        details.AddRange(BuildOfferDetails(existingOffer, currentOffer));
        return details.Where(detail => !ValuesMatch(detail.OldValue, detail.NewValue));
    }

    private static IEnumerable<SemanticChangeDetail> BuildOfferDetails(ProductOffer? oldOffer, ProductOffer? newOffer)
    {
        yield return new SemanticChangeDetail
        {
            AttributeKey = "offer.price",
            OldValue = oldOffer?.Price,
            NewValue = newOffer?.Price,
            ChangeType = "offer"
        };

        yield return new SemanticChangeDetail
        {
            AttributeKey = "offer.availability",
            OldValue = oldOffer?.Availability,
            NewValue = newOffer?.Availability,
            ChangeType = "offer"
        };
    }

    private static void AppendDirectChange(List<ProductChangeEvent> changeEvents, object? oldValue, object? newValue, string attributeKey, string canonicalProductId, string categoryKey, string sourceName, DateTime timestampUtc)
    {
        if (ValuesMatch(oldValue, newValue))
        {
            return;
        }

        changeEvents.Add(new ProductChangeEvent
        {
            Id = $"change:{canonicalProductId}:{attributeKey}:{timestampUtc:yyyyMMddHHmmssfff}:{changeEvents.Count + 1}",
            CanonicalProductId = canonicalProductId,
            CategoryKey = categoryKey,
            AttributeKey = attributeKey,
            OldValue = oldValue,
            NewValue = newValue,
            SourceName = sourceName,
            TimestampUtc = timestampUtc
        });
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