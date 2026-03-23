using System.Text.RegularExpressions;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Discovery;

public sealed partial class ListingPageClassifier(ProductLinkExtractor productLinkExtractor, DiscoveryLinkPolicy discoveryLinkPolicy)
{
    public ListingPageClassificationResult Classify(CrawlSource source, string categoryKey, string url, string html, int childDepth)
    {
        ArgumentNullException.ThrowIfNull(source);

        var links = productLinkExtractor.Extract(source, categoryKey, html, url, childDepth);
        decimal score = 0;
        var reasons = new List<string>();

        if (IsLikelyListingUrl(source, url))
        {
            score += 0.35m;
            reasons.Add("URL matches configured listing patterns.");
        }

        if (links.ProductLinks.Count >= 3)
        {
            score += 0.4m;
            reasons.Add("Multiple product links were extracted.");
        }
        else if (links.ProductLinks.Count > 0)
        {
            score += 0.2m;
            reasons.Add("At least one product link was extracted.");
        }

        if (links.PaginationLinks.Count > 0)
        {
            score += 0.2m;
            reasons.Add("Pagination links were extracted.");
        }

        if (links.CategoryLinks.Count > 0)
        {
            score += 0.15m;
            reasons.Add("Category navigation links were extracted.");
        }

        if (!string.IsNullOrWhiteSpace(html) && ListingMarkerRegex().IsMatch(html))
        {
            score += 0.15m;
            reasons.Add("Listing page markers were detected in markup.");
        }

        return new ListingPageClassificationResult(
            IsListingPage: score >= 0.45m,
            Confidence: score,
            Reason: reasons.Count == 0 ? "No listing signals matched." : string.Join(" ", reasons),
            Links: links);
    }

    public bool IsLikelyListingUrl(CrawlSource source, string url)
    {
        if (discoveryLinkPolicy.MatchesListingPattern(source, url))
        {
            return true;
        }

        var path = new Uri(url, UriKind.Absolute).AbsolutePath;
        return path.Contains("/category/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/categories/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/collections/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/browse/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/search", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("pagination|product-grid|product-list|category-title|filters|sort-by|results", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ListingMarkerRegex();
}

public sealed record ListingPageClassificationResult(
    bool IsListingPage,
    decimal Confidence,
    string Reason,
    ProductLinkExtractionResult Links);