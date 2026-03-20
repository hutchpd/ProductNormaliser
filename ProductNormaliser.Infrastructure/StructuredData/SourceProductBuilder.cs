using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.StructuredData;

public sealed class SourceProductBuilder
{
    public SourceProduct Build(
        string sourceName,
        string categoryKey,
        ExtractedStructuredProduct extractedProduct,
        DateTime fetchedUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryKey);
        ArgumentNullException.ThrowIfNull(extractedProduct);

        return new SourceProduct
        {
            Id = BuildId(sourceName, extractedProduct),
            SourceName = sourceName,
            SourceUrl = extractedProduct.SourceUrl,
            CategoryKey = categoryKey,
            Brand = extractedProduct.Brand,
            ModelNumber = extractedProduct.ModelNumber,
            Gtin = extractedProduct.Gtin,
            Title = extractedProduct.Name,
            RawAttributes = BuildRawAttributes(extractedProduct.Attributes),
            Offers = BuildOffers(sourceName, extractedProduct.SourceUrl, extractedProduct.Offers, fetchedUtc),
            RawSchemaJson = extractedProduct.RawJson,
            FetchedUtc = fetchedUtc
        };
    }

    private static string BuildId(string sourceName, ExtractedStructuredProduct extractedProduct)
    {
        var identity = string.Join(
            "|",
            sourceName,
            extractedProduct.SourceUrl,
            extractedProduct.Gtin ?? string.Empty,
            extractedProduct.ModelNumber ?? string.Empty,
            extractedProduct.Name ?? string.Empty);

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return $"{sourceName}:{Convert.ToHexString(hashBytes)[..16]}";
    }

    private static Dictionary<string, SourceAttributeValue> BuildRawAttributes(IReadOnlyDictionary<string, string> attributes)
    {
        var rawAttributes = new Dictionary<string, SourceAttributeValue>(StringComparer.OrdinalIgnoreCase);

        foreach (var attribute in attributes)
        {
            rawAttributes[attribute.Key] = new SourceAttributeValue
            {
                AttributeKey = attribute.Key,
                Value = attribute.Value,
                ValueType = InferValueType(attribute.Value),
                SourcePath = "jsonld.additionalProperty"
            };
        }

        return rawAttributes;
    }

    private static List<ProductOffer> BuildOffers(
        string sourceName,
        string sourceUrl,
        IReadOnlyList<ExtractedOffer> offers,
        DateTime fetchedUtc)
    {
        var productOffers = new List<ProductOffer>(offers.Count);

        for (var index = 0; index < offers.Count; index++)
        {
            var offer = offers[index];
            productOffers.Add(new ProductOffer
            {
                Id = $"{sourceName}:offer:{index + 1}",
                SourceName = sourceName,
                SourceUrl = sourceUrl,
                Price = offer.Price,
                Currency = offer.Currency,
                Availability = offer.Availability,
                ObservedUtc = fetchedUtc
            });
        }

        return productOffers;
    }

    private static string InferValueType(string value)
    {
        if (bool.TryParse(value, out _))
        {
            return "boolean";
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            return "integer";
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
        {
            return "decimal";
        }

        return "string";
    }
}