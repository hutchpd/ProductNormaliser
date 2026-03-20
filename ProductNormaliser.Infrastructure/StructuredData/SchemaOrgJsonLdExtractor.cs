using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.StructuredData;

public sealed class SchemaOrgJsonLdExtractor : IStructuredDataExtractor
{
    private static readonly Regex JsonLdScriptPattern = new(
        "<script\\b[^>]*type\\s*=\\s*[\"']application/ld\\+json[\"'][^>]*>(?<json>.*?)</script>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    public IReadOnlyCollection<ExtractedStructuredProduct> ExtractProducts(string html, string url)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var products = new List<ExtractedStructuredProduct>();
        var scriptMatches = JsonLdScriptPattern.Matches(html);

        foreach (Match scriptMatch in scriptMatches)
        {
            var rawJson = WebUtility.HtmlDecode(scriptMatch.Groups["json"].Value).Trim();
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(rawJson);
                products.AddRange(ExtractProductsFromDocument(document.RootElement, url));
            }
            catch (JsonException)
            {
                continue;
            }
        }

        return products;
    }

    private static IReadOnlyCollection<ExtractedStructuredProduct> ExtractProductsFromDocument(JsonElement root, string url)
    {
        var nodes = FlattenNodes(root).ToArray();
        var offersById = nodes
            .Where(node => HasSchemaType(node, "Offer"))
            .Select(node => new KeyValuePair<string, JsonElement?>(GetId(node) ?? string.Empty, node))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value!.Value, StringComparer.Ordinal);

        var results = new List<ExtractedStructuredProduct>();
        foreach (var node in nodes.Where(node => HasSchemaType(node, "Product")))
        {
            results.Add(ExtractProduct(node, url, offersById));
        }

        return results;
    }

    private static IEnumerable<JsonElement> FlattenNodes(JsonElement node)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var child in node.EnumerateArray())
                {
                    foreach (var grandChild in FlattenNodes(child))
                    {
                        yield return grandChild;
                    }
                }

                break;

            case JsonValueKind.Object:
                if (TryGetProperty(node, "@graph", out var graphNode))
                {
                    foreach (var graphChild in FlattenNodes(graphNode))
                    {
                        yield return graphChild;
                    }

                    yield break;
                }

                yield return node.Clone();
                break;
        }
    }

    private static ExtractedStructuredProduct ExtractProduct(
        JsonElement productNode,
        string url,
        IReadOnlyDictionary<string, JsonElement> offersById)
    {
        var extractedProduct = new ExtractedStructuredProduct
        {
            SourceUrl = url,
            Name = FirstNonEmptyString(productNode, "name", "title"),
            Brand = ExtractBrand(productNode),
            Gtin = FirstNonEmptyString(productNode, "gtin", "gtin13", "gtin14", "gtin12", "gtin8"),
            ModelNumber = FirstNonEmptyString(productNode, "model", "mpn", "sku"),
            RawJson = productNode.GetRawText()
        };

        AddIdentityAttributes(productNode, extractedProduct.Attributes);
        AddAdditionalProperties(productNode, extractedProduct.Attributes);
        extractedProduct.Offers.AddRange(ExtractOffers(productNode, offersById));

        return extractedProduct;
    }

    private static void AddIdentityAttributes(JsonElement productNode, IDictionary<string, string> attributes)
    {
        AddIfPresent(attributes, "sku", FirstNonEmptyString(productNode, "sku"));
        AddIfPresent(attributes, "mpn", FirstNonEmptyString(productNode, "mpn"));
        AddIfPresent(attributes, "model", FirstNonEmptyString(productNode, "model"));
    }

    private static void AddAdditionalProperties(JsonElement productNode, IDictionary<string, string> attributes)
    {
        if (!TryGetProperty(productNode, "additionalProperty", out var additionalPropertyNode))
        {
            return;
        }

        foreach (var propertyNode in EnumerateNodes(additionalPropertyNode))
        {
            var key = FirstNonEmptyString(propertyNode, "name", "propertyID");
            var value = TryGetProperty(propertyNode, "value", out var valueNode)
                ? ConvertElementToString(valueNode)
                : null;

            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            attributes[key] = value;
        }
    }

    private static IReadOnlyCollection<ExtractedOffer> ExtractOffers(
        JsonElement productNode,
        IReadOnlyDictionary<string, JsonElement> offersById)
    {
        if (!TryGetProperty(productNode, "offers", out var offersNode))
        {
            return [];
        }

        var results = new List<ExtractedOffer>();
        foreach (var node in EnumerateNodes(offersNode))
        {
            var resolvedNode = ResolveOfferNode(node, offersById);
            if (resolvedNode is null || !HasSchemaType(resolvedNode.Value, "Offer"))
            {
                continue;
            }

            results.Add(new ExtractedOffer
            {
                Price = TryGetDecimal(resolvedNode.Value, "price"),
                Currency = FirstNonEmptyString(resolvedNode.Value, "priceCurrency"),
                Availability = FirstNonEmptyString(resolvedNode.Value, "availability"),
                RawJson = resolvedNode.Value.GetRawText()
            });
        }

        return results;
    }

    private static JsonElement? ResolveOfferNode(JsonElement node, IReadOnlyDictionary<string, JsonElement> offersById)
    {
        if (HasSchemaType(node, "Offer"))
        {
            return node;
        }

        var offerId = GetId(node);
        if (!string.IsNullOrWhiteSpace(offerId) && offersById.TryGetValue(offerId, out var resolvedOffer))
        {
            return resolvedOffer;
        }

        return null;
    }

    private static IEnumerable<JsonElement> EnumerateNodes(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in node.EnumerateArray())
            {
                yield return child;
            }

            yield break;
        }

        if (node.ValueKind == JsonValueKind.Object)
        {
            yield return node;
        }
    }

    private static bool HasSchemaType(JsonElement node, string expectedType)
    {
        if (!TryGetProperty(node, "@type", out var typeNode))
        {
            return false;
        }

        return typeNode.ValueKind switch
        {
            JsonValueKind.String => MatchesType(typeNode.GetString(), expectedType),
            JsonValueKind.Array => typeNode.EnumerateArray().Any(typeValue => MatchesType(typeValue.GetString(), expectedType)),
            _ => false
        };
    }

    private static bool MatchesType(string? actualType, string expectedType)
    {
        if (string.IsNullOrWhiteSpace(actualType))
        {
            return false;
        }

        var lastSlashIndex = actualType.LastIndexOf('/');
        var lastHashIndex = actualType.LastIndexOf('#');
        var separatorIndex = Math.Max(lastSlashIndex, lastHashIndex);
        var simplifiedType = separatorIndex >= 0
            ? actualType[(separatorIndex + 1)..]
            : actualType;

        return string.Equals(simplifiedType, expectedType, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetProperty(JsonElement node, string propertyName, out JsonElement value)
    {
        foreach (var property in node.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? ExtractBrand(JsonElement productNode)
    {
        if (!TryGetProperty(productNode, "brand", out var brandNode))
        {
            return null;
        }

        return brandNode.ValueKind switch
        {
            JsonValueKind.String => brandNode.GetString(),
            JsonValueKind.Object => FirstNonEmptyString(brandNode, "name"),
            JsonValueKind.Array => brandNode
                .EnumerateArray()
                .Select(node => node.ValueKind == JsonValueKind.String ? node.GetString() : FirstNonEmptyString(node, "name"))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
            _ => null
        };
    }

    private static string? FirstNonEmptyString(JsonElement node, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!TryGetProperty(node, propertyName, out var valueNode))
            {
                continue;
            }

            var value = ConvertElementToString(valueNode);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? ConvertElementToString(JsonElement valueNode)
    {
        return valueNode.ValueKind switch
        {
            JsonValueKind.String => valueNode.GetString(),
            JsonValueKind.Number => valueNode.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Object when TryGetProperty(valueNode, "@value", out var nestedValueNode) => ConvertElementToString(nestedValueNode),
            JsonValueKind.Object when TryGetProperty(valueNode, "name", out var nameNode) => ConvertElementToString(nameNode),
            _ => null
        };
    }

    private static decimal? TryGetDecimal(JsonElement node, string propertyName)
    {
        var rawValue = FirstNonEmptyString(node, propertyName);
        return decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static string? GetId(JsonElement node)
    {
        return FirstNonEmptyString(node, "@id");
    }

    private static void AddIfPresent(IDictionary<string, string> attributes, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            attributes[key] = value;
        }
    }
}