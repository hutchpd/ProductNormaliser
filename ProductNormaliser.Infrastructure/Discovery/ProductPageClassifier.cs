using System.Text.RegularExpressions;
using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Discovery;

public sealed partial class ProductPageClassifier(IStructuredDataExtractor structuredDataExtractor, DiscoveryLinkPolicy discoveryLinkPolicy)
{
    public ProductPageClassificationResult Classify(CrawlSource source, string url, string html)
    {
        ArgumentNullException.ThrowIfNull(source);

        var structuredProducts = string.IsNullOrWhiteSpace(html)
            ? []
            : structuredDataExtractor.ExtractProducts(html, url);

        if (structuredProducts.Count > 0)
        {
            return new ProductPageClassificationResult(
                IsProductPage: true,
                Confidence: 1.0m,
                Reason: "JSON-LD Product data detected.",
                StructuredProductCount: structuredProducts.Count);
        }

        decimal score = 0;
        var reasons = new List<string>();

        if (IsLikelyProductUrl(source, url))
        {
            score += 0.45m;
            reasons.Add("URL matches configured product patterns.");
        }

        if (!string.IsNullOrWhiteSpace(html) && ProductMetaRegex().IsMatch(html))
        {
            score += 0.3m;
            reasons.Add("Page metadata identifies a product.");
        }

        if (!string.IsNullOrWhiteSpace(html) && CommerceSignalsRegex().IsMatch(html))
        {
            score += 0.2m;
            reasons.Add("Commerce signals detected in markup.");
        }

        if (!string.IsNullOrWhiteSpace(html) && PriceSignalsRegex().IsMatch(html))
        {
            score += 0.15m;
            reasons.Add("Price or offer signals detected.");
        }

        return new ProductPageClassificationResult(
            IsProductPage: score >= 0.6m,
            Confidence: score,
            Reason: reasons.Count == 0 ? "No product signals matched." : string.Join(" ", reasons),
            StructuredProductCount: 0);
    }

    public bool IsLikelyProductUrl(CrawlSource source, string url)
    {
        if (discoveryLinkPolicy.MatchesProductPattern(source, url))
        {
            return true;
        }

        var path = new Uri(url, UriKind.Absolute).AbsolutePath;
        return path.Contains("/product/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/products/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/p/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/item/", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("og:type[\"'=\\s:>]+product|itemtype=[\"'][^\"']*Product|data-product", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProductMetaRegex();

    [GeneratedRegex("add[- ]to[- ](cart|basket)|buy[- ]now|sku|mpn|gtin|model", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CommerceSignalsRegex();

    [GeneratedRegex("price|priceCurrency|availability|InStock|Offer", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PriceSignalsRegex();
}

public sealed record ProductPageClassificationResult(
    bool IsProductPage,
    decimal Confidence,
    string Reason,
    int StructuredProductCount);