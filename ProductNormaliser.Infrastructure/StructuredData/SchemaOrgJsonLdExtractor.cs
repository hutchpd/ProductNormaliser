using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.StructuredData;

public sealed class SchemaOrgJsonLdExtractor : IStructuredDataExtractor
{
    private static readonly HashSet<string> ReservedAttributeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "name",
        "title",
        "brand",
        "model",
        "mpn",
        "sku",
        "gtin",
        "gtin8",
        "gtin12",
        "gtin13",
        "gtin14",
        "price",
        "pricecurrency",
        "availability",
        "offers"
    };

    private static readonly Regex JsonLdScriptPattern = new(
        "<script\\b[^>]*type\\s*=\\s*[\"']application/ld\\+json[\"'][^>]*>(?<json>.*?)</script>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex CommerceSignalsPattern = new(
        "add\\s+to\\s+(cart|basket)|buy\\s+now|in\\s+stock|price|sku|mpn|gtin|model",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex PricePattern = new(
        "(?<currency>GBP|EUR|USD|£|€|\\$)\\s*(?<amount>\\d[\\d,]*(?:\\.\\d{1,2})?)|(?<amountOnly>\\d[\\d,]*(?:\\.\\d{1,2})?)\\s*(?<currencyCode>GBP|EUR|USD)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TableRowPattern = new(
        "<tr[^>]*>\\s*<(?:th|td)[^>]*>(?<key>.*?)</(?:th|td)>\\s*<(?:td|th)[^>]*>(?<value>.*?)</(?:td|th)>\\s*</tr>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex HtmlTagPattern = new(
        "<[^>]+>",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);

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
                using var jsonDocument = JsonDocument.Parse(rawJson);
                products.AddRange(ExtractProductsFromDocument(jsonDocument.RootElement, url));
            }
            catch (JsonException)
            {
                continue;
            }
        }

        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(html);

        MergeProducts(products, ExtractProductsFromMicrodata(htmlDocument, url));

        if (products.Count == 0)
        {
            MergeProducts(products, ExtractProductsFromSpecTables(htmlDocument, html, url));
        }

        return products;
    }

    private static void MergeProducts(ICollection<ExtractedStructuredProduct> existingProducts, IEnumerable<ExtractedStructuredProduct> newProducts)
    {
        foreach (var product in newProducts)
        {
            if (!LooksLikeProduct(product))
            {
                continue;
            }

            var existing = existingProducts.FirstOrDefault(candidate => AreEquivalent(candidate, product));
            if (existing is null)
            {
                existingProducts.Add(product);
                continue;
            }

            MergeInto(existing, product);
        }
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

    private static IReadOnlyCollection<ExtractedStructuredProduct> ExtractProductsFromMicrodata(HtmlDocument document, string url)
    {
        var productNodes = document.DocumentNode.SelectNodes("//*[@itemscope and @itemtype]");
        if (productNodes is null || productNodes.Count == 0)
        {
            return [];
        }

        var results = new List<ExtractedStructuredProduct>();
        foreach (var productNode in productNodes.Where(IsMicrodataProductNode))
        {
            var product = new ExtractedStructuredProduct
            {
                SourceUrl = url,
                Name = FirstMicrodataValue(productNode, "name", "title"),
                Brand = ExtractMicrodataBrand(productNode),
                Gtin = FirstMicrodataValue(productNode, "gtin", "gtin13", "gtin14", "gtin12", "gtin8"),
                ModelNumber = FirstMicrodataValue(productNode, "model", "mpn", "sku"),
                RawJson = productNode.OuterHtml
            };

            AddIfPresent(product.Attributes, "sku", FirstMicrodataValue(productNode, "sku"));
            AddIfPresent(product.Attributes, "mpn", FirstMicrodataValue(productNode, "mpn"));
            AddIfPresent(product.Attributes, "model", FirstMicrodataValue(productNode, "model"));

            AddMicrodataAdditionalProperties(productNode, product.Attributes);
            AddMicrodataLooseAttributes(productNode, product.Attributes);
            product.Offers.AddRange(ExtractMicrodataOffers(productNode));

            results.Add(product);
        }

        return results;
    }

    private static IReadOnlyCollection<ExtractedStructuredProduct> ExtractProductsFromSpecTables(HtmlDocument document, string html, string url)
    {
        var title = ExtractSpecFallbackTitle(document);
        if (string.IsNullOrWhiteSpace(title))
        {
            return [];
        }

        var attributes = ExtractSpecTableAttributes(document);
        if (attributes.Count == 0)
        {
            foreach (var attribute in ExtractSpecTableAttributesFromMarkup(html))
            {
                attributes[attribute.Key] = attribute.Value;
            }
        }

        var brand = FindAttributeValue(attributes, "brand", "manufacturer");
        var model = FindAttributeValue(attributes, "model", "model number", "model no", "mpn", "sku");
        var gtin = FindAttributeValue(attributes, "gtin", "ean", "upc");
        var offer = ExtractFallbackOffer(document);

        if (attributes.Count < 2 && string.IsNullOrWhiteSpace(model) && string.IsNullOrWhiteSpace(gtin))
        {
            return [];
        }

        if (!HasCommerceSignals(document))
        {
            return [];
        }

        var product = new ExtractedStructuredProduct
        {
            SourceUrl = url,
            Name = title,
            Brand = brand,
            ModelNumber = model,
            Gtin = gtin,
            RawJson = document.DocumentNode.OuterHtml
        };

        foreach (var attribute in attributes)
        {
            product.Attributes[attribute.Key] = attribute.Value;
        }

        if (offer is not null)
        {
            product.Offers.Add(offer);
        }

        return [product];
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

    private static bool IsMicrodataProductNode(HtmlNode node)
    {
        var itemType = node.GetAttributeValue("itemtype", string.Empty);
        if (string.IsNullOrWhiteSpace(itemType))
        {
            return false;
        }

        return itemType.Contains("product", StringComparison.OrdinalIgnoreCase)
            && !itemType.Contains("offer", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FirstMicrodataValue(HtmlNode root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var node = FindFirstItemPropNode(root, propertyName);
            var value = GetNodeValue(node);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static HtmlNode? FindFirstItemPropNode(HtmlNode root, string propertyName)
    {
        return root
            .DescendantsAndSelf()
            .FirstOrDefault(node => HasItemProp(node, propertyName));
    }

    private static bool HasItemProp(HtmlNode node, string propertyName)
    {
        var itemProp = node.GetAttributeValue("itemprop", string.Empty);
        if (string.IsNullOrWhiteSpace(itemProp))
        {
            return false;
        }

        return itemProp
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(token => string.Equals(token, propertyName, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ExtractMicrodataBrand(HtmlNode root)
    {
        var brandNode = FindFirstItemPropNode(root, "brand");
        if (brandNode is null)
        {
            return null;
        }

        if (brandNode.Attributes["itemscope"] is not null)
        {
            return FirstMicrodataValue(brandNode, "name") ?? GetNodeValue(brandNode);
        }

        return GetNodeValue(brandNode);
    }

    private static void AddMicrodataAdditionalProperties(HtmlNode root, IDictionary<string, string> attributes)
    {
        foreach (var propertyNode in root.Descendants().Where(node => HasItemProp(node, "additionalProperty")))
        {
            var key = FirstMicrodataValue(propertyNode, "name", "propertyID");
            var value = FirstMicrodataValue(propertyNode, "value") ?? GetNodeValue(propertyNode);
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            attributes[key] = value;
        }
    }

    private static void AddMicrodataLooseAttributes(HtmlNode root, IDictionary<string, string> attributes)
    {
        foreach (var node in root.Descendants())
        {
            var itemProp = node.GetAttributeValue("itemprop", string.Empty);
            if (string.IsNullOrWhiteSpace(itemProp))
            {
                continue;
            }

            foreach (var propertyName in itemProp.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (ReservedAttributeKeys.Contains(propertyName))
                {
                    continue;
                }

                var value = GetNodeValue(node);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    attributes[propertyName] = value;
                }
            }
        }
    }

    private static IReadOnlyCollection<ExtractedOffer> ExtractMicrodataOffers(HtmlNode root)
    {
        var offerNodes = root.Descendants().Where(node => HasItemProp(node, "offers")).ToArray();
        if (offerNodes.Length == 0)
        {
            return [];
        }

        var offers = new List<ExtractedOffer>();
        foreach (var offerNode in offerNodes)
        {
            var priceText = FirstMicrodataValue(offerNode, "price");
            var currency = FirstMicrodataValue(offerNode, "priceCurrency");
            var availability = FirstMicrodataValue(offerNode, "availability");

            offers.Add(new ExtractedOffer
            {
                Price = TryParseDecimal(priceText),
                Currency = currency,
                Availability = availability,
                RawJson = offerNode.OuterHtml
            });
        }

        return offers
            .Where(offer => offer.Price is not null || !string.IsNullOrWhiteSpace(offer.Currency) || !string.IsNullOrWhiteSpace(offer.Availability))
            .ToArray();
    }

    private static string? ExtractSpecFallbackTitle(HtmlDocument document)
    {
        var h1 = document.DocumentNode.SelectSingleNode("//h1[normalize-space()]");
        if (!string.IsNullOrWhiteSpace(h1?.InnerText))
        {
            return NormalizeText(h1.InnerText);
        }

        var ogTitle = document.DocumentNode.SelectSingleNode("//meta[@property='og:title' or @name='og:title']");
        return GetNodeValue(ogTitle);
    }

    private static Dictionary<string, string> ExtractSpecTableAttributes(HtmlDocument document)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var tableRows = document.DocumentNode.SelectNodes("//table//tr");
        if (tableRows is not null)
        {
            foreach (var rowNode in tableRows)
            {
                var keyNode = rowNode.SelectSingleNode("./th") ?? rowNode.SelectSingleNode("./td[1]");
                var valueNodeCandidate = rowNode.SelectSingleNode("./td[2]") ?? rowNode.SelectSingleNode("./th[2]");
                AddSpecAttribute(attributes, keyNode?.InnerText, valueNodeCandidate?.InnerText);
            }
        }

        var definitionTerms = document.DocumentNode.SelectNodes("//dl/dt");
        if (definitionTerms is not null)
        {
            foreach (var rowNode in definitionTerms)
            {
                var valueNode = rowNode.SelectSingleNode("following-sibling::dd[1]");
                AddSpecAttribute(attributes, rowNode.InnerText, valueNode?.InnerText);
            }
        }

        var blockNodes = document.DocumentNode.SelectNodes("//*[@class or @id]");
        if (blockNodes is not null)
        {
            foreach (var block in blockNodes.Where(HasSpecBlockMarker))
            {
                foreach (var row in EnumerateInlineSpecRows(block))
                {
                    var text = NormalizeText(row.InnerText);
                    var separatorIndex = text.IndexOf(':');
                    if (separatorIndex <= 0 || separatorIndex >= text.Length - 1)
                    {
                        continue;
                    }

                    AddSpecAttribute(attributes, text[..separatorIndex], text[(separatorIndex + 1)..]);
                }
            }
        }

        return attributes;
    }

    private static Dictionary<string, string> ExtractSpecTableAttributesFromMarkup(string html)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in TableRowPattern.Matches(html))
        {
            var rawKey = StripTags(match.Groups["key"].Value);
            var rawValue = StripTags(match.Groups["value"].Value);
            AddSpecAttribute(attributes, rawKey, rawValue);
        }

        return attributes;
    }

    private static IEnumerable<HtmlNode> EnumerateInlineSpecRows(HtmlNode block)
    {
        foreach (var xpath in new[] { ".//li[contains(., ':')]", ".//p[contains(., ':')]", ".//div[contains(., ':')]", ".//span[contains(., ':')]" })
        {
            var nodes = block.SelectNodes(xpath);
            if (nodes is null)
            {
                continue;
            }

            foreach (var node in nodes)
            {
                yield return node;
            }
        }
    }

    private static bool HasSpecBlockMarker(HtmlNode node)
    {
        var marker = $"{node.GetAttributeValue("class", string.Empty)} {node.GetAttributeValue("id", string.Empty)}";
        return marker.Contains("spec", StringComparison.OrdinalIgnoreCase)
            || marker.Contains("tech", StringComparison.OrdinalIgnoreCase)
            || marker.Contains("attribute", StringComparison.OrdinalIgnoreCase)
            || marker.Contains("detail", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddSpecAttribute(IDictionary<string, string> attributes, string? rawKey, string? rawValue)
    {
        var key = NormalizeText(rawKey);
        var value = NormalizeText(rawValue);
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (key.Length > 80 || value.Length > 250)
        {
            return;
        }

        attributes[key] = value;
    }

    private static string? FindAttributeValue(IReadOnlyDictionary<string, string> attributes, params string[] keys)
    {
        foreach (var key in keys)
        {
            var match = attributes.FirstOrDefault(entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match.Value))
            {
                return match.Value;
            }
        }

        return null;
    }

    private static ExtractedOffer? ExtractFallbackOffer(HtmlDocument document)
    {
        var priceNode = document.DocumentNode.SelectSingleNode("//*[@itemprop='price' or contains(@class, 'price') or contains(@data-test, 'price')][normalize-space()]");
        var priceText = GetNodeValue(priceNode);
        if (string.IsNullOrWhiteSpace(priceText))
        {
            priceText = PricePattern.Match(document.DocumentNode.InnerText).Value;
        }

        var price = TryParsePrice(priceText, out var currency);
        var availabilityNode = document.DocumentNode.SelectSingleNode("//*[@itemprop='availability' or contains(@class, 'stock') or contains(@class, 'availability')][normalize-space()]");
        var availability = GetNodeValue(availabilityNode);

        if (price is null && string.IsNullOrWhiteSpace(availability))
        {
            return null;
        }

        return new ExtractedOffer
        {
            Price = price,
            Currency = currency,
            Availability = availability,
            RawJson = priceNode?.OuterHtml ?? availabilityNode?.OuterHtml ?? string.Empty
        };
    }

    private static bool HasCommerceSignals(HtmlDocument document)
    {
        var text = NormalizeText(document.DocumentNode.InnerText);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return CommerceSignalsPattern.IsMatch(text)
            || document.DocumentNode.SelectSingleNode("//*[@itemprop='price' or @itemprop='offers' or contains(@class, 'price') or contains(@class, 'add-to-cart') or contains(@class, 'buy-now')]") is not null;
    }

    private static string? GetNodeValue(HtmlNode? node)
    {
        if (node is null)
        {
            return null;
        }

        var content = node.GetAttributeValue("content", string.Empty);
        var href = node.GetAttributeValue("href", string.Empty);
        var src = node.GetAttributeValue("src", string.Empty);
        var candidate = string.IsNullOrWhiteSpace(content)
            ? string.IsNullOrWhiteSpace(href)
                ? src
                : href
            : content;

        if (!string.IsNullOrWhiteSpace(candidate))
        {
            return NormalizeText(candidate);
        }

        return NormalizeText(node.InnerText);
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decoded = WebUtility.HtmlDecode(value);
        return Regex.Replace(decoded, "\\s+", " ").Trim();
    }

    private static string StripTags(string value)
    {
        return NormalizeText(HtmlTagPattern.Replace(value, " "));
    }

    private static decimal? TryParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Replace(",", string.Empty, StringComparison.Ordinal).Trim();
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static decimal? TryParsePrice(string? value, out string? currency)
    {
        currency = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = PricePattern.Match(value);
        if (!match.Success)
        {
            return TryParseDecimal(value);
        }

        currency = NormalizeCurrency(match.Groups["currency"].Value, match.Groups["currencyCode"].Value);
        var amount = match.Groups["amount"].Success ? match.Groups["amount"].Value : match.Groups["amountOnly"].Value;
        return TryParseDecimal(amount);
    }

    private static string? NormalizeCurrency(string? symbol, string? currencyCode)
    {
        if (!string.IsNullOrWhiteSpace(currencyCode))
        {
            return currencyCode.ToUpperInvariant();
        }

        return symbol switch
        {
            "£" => "GBP",
            "€" => "EUR",
            "$" => "USD",
            _ => string.IsNullOrWhiteSpace(symbol) ? null : symbol.ToUpperInvariant()
        };
    }

    private static bool LooksLikeProduct(ExtractedStructuredProduct product)
    {
        if (string.IsNullOrWhiteSpace(product.Name))
        {
            return false;
        }

        return product.Attributes.Count >= 2
            || product.Offers.Count > 0
            || !string.IsNullOrWhiteSpace(product.ModelNumber)
            || !string.IsNullOrWhiteSpace(product.Gtin);
    }

    private static bool AreEquivalent(ExtractedStructuredProduct left, ExtractedStructuredProduct right)
    {
        if (!string.IsNullOrWhiteSpace(left.Gtin) && !string.IsNullOrWhiteSpace(right.Gtin))
        {
            return string.Equals(left.Gtin, right.Gtin, StringComparison.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrWhiteSpace(left.ModelNumber) && !string.IsNullOrWhiteSpace(right.ModelNumber))
        {
            var sameModel = string.Equals(left.ModelNumber, right.ModelNumber, StringComparison.OrdinalIgnoreCase);
            var sameBrand = string.Equals(left.Brand ?? string.Empty, right.Brand ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            if (sameModel && sameBrand)
            {
                return true;
            }
        }

        return string.Equals(left.Name ?? string.Empty, right.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.Brand ?? string.Empty, right.Brand ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static void MergeInto(ExtractedStructuredProduct existing, ExtractedStructuredProduct incoming)
    {
        existing.Name ??= incoming.Name;
        existing.Brand ??= incoming.Brand;
        existing.Gtin ??= incoming.Gtin;
        existing.ModelNumber ??= incoming.ModelNumber;

        foreach (var attribute in incoming.Attributes)
        {
            existing.Attributes[attribute.Key] = attribute.Value;
        }

        foreach (var offer in incoming.Offers)
        {
            if (existing.Offers.Any(candidate => candidate.Price == offer.Price
                && string.Equals(candidate.Currency, offer.Currency, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.Availability, offer.Availability, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            existing.Offers.Add(offer);
        }

        if (string.IsNullOrWhiteSpace(existing.RawJson) || incoming.RawJson.Length > existing.RawJson.Length)
        {
            existing.RawJson = incoming.RawJson;
        }
    }

    private static void AddIfPresent(IDictionary<string, string> attributes, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            attributes[key] = value;
        }
    }
}