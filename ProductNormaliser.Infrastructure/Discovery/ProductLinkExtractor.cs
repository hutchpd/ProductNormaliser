using System.Net;
using System.Text.RegularExpressions;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Discovery;

public sealed partial class ProductLinkExtractor(DiscoveryLinkPolicy discoveryLinkPolicy)
{
    public ProductLinkExtractionResult Extract(CrawlSource source, string categoryKey, string html, string baseUrl, int depth)
    {
        ArgumentNullException.ThrowIfNull(source);

        var result = new ProductLinkExtractionResult();
        if (string.IsNullOrWhiteSpace(html))
        {
            return result;
        }

        var baseUri = new Uri(baseUrl, UriKind.Absolute);
        var currentPage = discoveryLinkPolicy.NormalizeUrl(baseUri.ToString());

        foreach (Match match in AnchorRegex().Matches(html))
        {
            AddCandidate(source, categoryKey, baseUri, currentPage, depth, match.Groups["href"].Value, match.Value, match.Groups["text"].Value, result);
        }

        foreach (Match match in LinkRegex().Matches(html))
        {
            AddCandidate(source, categoryKey, baseUri, currentPage, depth, match.Groups["href"].Value, match.Value, string.Empty, result);
        }

        return result;
    }

    private void AddCandidate(
        CrawlSource source,
        string categoryKey,
        Uri baseUri,
        string currentPage,
        int depth,
        string href,
        string rawMarkup,
        string rawText,
        ProductLinkExtractionResult result)
    {
        var candidate = WebUtility.HtmlDecode(href).Trim();
        if (candidate.Length == 0
            || candidate.StartsWith("#", StringComparison.Ordinal)
            || candidate.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
            || !Uri.TryCreate(baseUri, candidate, out var absoluteUri)
            || !discoveryLinkPolicy.TryNormalizeAndValidate(source, categoryKey, absoluteUri.ToString(), depth, out var normalized)
            || string.Equals(normalized, currentPage, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var text = WebUtility.HtmlDecode(rawText).Trim();
        if (IsProductLink(source, normalized, rawMarkup, text))
        {
            result.AddProduct(normalized);
            return;
        }

        if (IsPaginationLink(normalized, rawMarkup, text))
        {
            result.AddPagination(normalized);
            return;
        }

        if (IsCategoryLink(source, normalized, rawMarkup, text))
        {
            result.AddCategory(normalized);
            return;
        }

        result.AddRelated(normalized);
    }

    private bool IsProductLink(CrawlSource source, string url, string rawMarkup, string text)
    {
        return discoveryLinkPolicy.MatchesProductPattern(source, url)
            || rawMarkup.Contains("product-card", StringComparison.OrdinalIgnoreCase)
            || rawMarkup.Contains("product-tile", StringComparison.OrdinalIgnoreCase)
            || rawMarkup.Contains("data-product", StringComparison.OrdinalIgnoreCase)
            || text.Contains("buy", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPaginationLink(string url, string rawMarkup, string text)
    {
        return rawMarkup.Contains("rel=\"next\"", StringComparison.OrdinalIgnoreCase)
            || rawMarkup.Contains("rel='next'", StringComparison.OrdinalIgnoreCase)
            || rawMarkup.Contains("rel=\"prev\"", StringComparison.OrdinalIgnoreCase)
            || rawMarkup.Contains("rel='prev'", StringComparison.OrdinalIgnoreCase)
            || url.Contains("?page=", StringComparison.OrdinalIgnoreCase)
            || url.Contains("&page=", StringComparison.OrdinalIgnoreCase)
            || url.Contains("/page/", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(text, "^(next|previous|prev|more|\\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private bool IsCategoryLink(CrawlSource source, string url, string rawMarkup, string text)
    {
        return discoveryLinkPolicy.MatchesListingPattern(source, url)
            || rawMarkup.Contains("category", StringComparison.OrdinalIgnoreCase)
            || rawMarkup.Contains("collection", StringComparison.OrdinalIgnoreCase)
            || rawMarkup.Contains("department", StringComparison.OrdinalIgnoreCase)
            || text.Contains("shop", StringComparison.OrdinalIgnoreCase)
            || text.Contains("browse", StringComparison.OrdinalIgnoreCase)
            || text.Contains("category", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("<a\\b(?<attrs>[^>]*)href=[\"'](?<href>[^\"'#>]+)[\"'][^>]*>(?<text>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex AnchorRegex();

    [GeneratedRegex("<link\\b(?<attrs>[^>]*)href=[\"'](?<href>[^\"'#>]+)[\"'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex LinkRegex();
}

public sealed class ProductLinkExtractionResult
{
    private readonly HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> allLinks = [];
    private readonly List<string> productLinks = [];
    private readonly List<string> relatedLinks = [];
    private readonly List<string> paginationLinks = [];
    private readonly List<string> categoryLinks = [];

    public IReadOnlyList<string> AllLinks => allLinks;
    public IReadOnlyList<string> ProductLinks => productLinks;
    public IReadOnlyList<string> RelatedLinks => relatedLinks;
    public IReadOnlyList<string> PaginationLinks => paginationLinks;
    public IReadOnlyList<string> CategoryLinks => categoryLinks;

    public void AddProduct(string url)
    {
        Add(url, productLinks);
    }

    public void AddRelated(string url)
    {
        Add(url, relatedLinks);
    }

    public void AddPagination(string url)
    {
        Add(url, paginationLinks);
    }

    public void AddCategory(string url)
    {
        Add(url, categoryLinks);
    }

    private void Add(string url, List<string> bucket)
    {
        if (!seen.Add(url))
        {
            return;
        }

        bucket.Add(url);
        allLinks.Add(url);
    }
}