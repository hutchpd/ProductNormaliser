using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Crawling;

namespace ProductNormaliser.Infrastructure.Sources;

public sealed partial class HttpSourceCandidateProbeService(IHttpFetcher httpFetcher, IOptions<SourceCandidateDiscoveryOptions> options) : ISourceCandidateProbeService
{
    private readonly SourceCandidateDiscoveryOptions options = options.Value;

    public async Task<SourceCandidateProbeResult> ProbeAsync(SourceCandidateSearchResult candidate, IReadOnlyCollection<string> categoryKeys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(categoryKeys);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.ProbeTimeoutSeconds)));

        var homePageTask = TryFetchTextAsync(candidate.BaseUrl, timeoutCts.Token);
        var robotsTask = TryFetchTextAsync(new Uri(new Uri(candidate.BaseUrl, UriKind.Absolute), "/robots.txt").ToString(), timeoutCts.Token);

        await Task.WhenAll(homePageTask, robotsTask);

        var homePageHtml = await homePageTask;
        var robotsText = await robotsTask;
        var sitemapUrls = ExtractSitemapUrls(candidate.BaseUrl, robotsText, homePageHtml);
        var categoryPageHints = ExtractCategoryPageHints(homePageHtml, categoryKeys);

        return new SourceCandidateProbeResult
        {
            HomePageReachable = homePageHtml is not null,
            RobotsTxtReachable = robotsText is not null,
            SitemapDetected = sitemapUrls.Count > 0,
            SitemapUrls = sitemapUrls,
            CategoryRelevanceScore = ScoreCategoryRelevance(categoryKeys, homePageHtml, categoryPageHints),
            CategoryPageHints = categoryPageHints,
            LikelyListingUrlPatterns = InferListingUrlPatterns(categoryPageHints),
            LikelyProductUrlPatterns = InferProductUrlPatterns(homePageHtml)
        };
    }

    private async Task<string?> TryFetchTextAsync(string absoluteUrl, CancellationToken cancellationToken)
    {
        var result = await httpFetcher.FetchAsync(new CrawlTarget
        {
            Url = absoluteUrl,
            CategoryKey = string.Empty
        }, cancellationToken);

        return result.IsSuccess ? result.Html ?? string.Empty : null;
    }

    private static IReadOnlyList<string> ExtractSitemapUrls(string baseUrl, string? robotsTxt, string? homePageHtml)
    {
        var baseUri = new Uri(baseUrl, UriKind.Absolute);
        var sitemapUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(robotsTxt))
        {
            foreach (Match match in SitemapDirectiveRegex().Matches(robotsTxt))
            {
                var candidate = match.Groups["url"].Value.Trim();
                if (Uri.TryCreate(baseUri, candidate, out var sitemapUri))
                {
                    sitemapUrls.Add(sitemapUri.ToString());
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(homePageHtml))
        {
            foreach (var href in ExtractHrefs(homePageHtml))
            {
                if (!href.Contains("sitemap", StringComparison.OrdinalIgnoreCase)
                    || !href.Contains(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Uri.TryCreate(baseUri, href, out var sitemapUri))
                {
                    sitemapUrls.Add(sitemapUri.ToString());
                }
            }
        }

        return sitemapUrls
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ExtractCategoryPageHints(string? homePageHtml, IReadOnlyCollection<string> categoryKeys)
    {
        if (string.IsNullOrWhiteSpace(homePageHtml) || categoryKeys.Count == 0)
        {
            return [];
        }

        var hints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var href in ExtractHrefs(homePageHtml))
        {
            var normalizedHref = href.Trim();
            if (string.IsNullOrWhiteSpace(normalizedHref))
            {
                continue;
            }

            if (normalizedHref.Contains("/category/", StringComparison.OrdinalIgnoreCase)
                || normalizedHref.Contains("/department/", StringComparison.OrdinalIgnoreCase)
                || normalizedHref.Contains("/shop/", StringComparison.OrdinalIgnoreCase)
                || normalizedHref.Contains("/browse/", StringComparison.OrdinalIgnoreCase))
            {
                hints.Add(normalizedHref);
                continue;
            }

            foreach (var categoryKey in categoryKeys)
            {
                if (normalizedHref.Contains(categoryKey, StringComparison.OrdinalIgnoreCase)
                    || normalizedHref.Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase).Contains(categoryKey.Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase), StringComparison.OrdinalIgnoreCase)
                    || normalizedHref.Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase).Contains(categoryKey.Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase), StringComparison.OrdinalIgnoreCase))
                {
                    hints.Add(normalizedHref);
                    break;
                }
            }
        }

        return hints
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();
    }

    private static IReadOnlyList<string> InferListingUrlPatterns(IEnumerable<string> categoryPageHints)
    {
        var patterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var hint in categoryPageHints)
        {
            if (!Uri.TryCreate(hint, UriKind.Absolute, out var uri) && !Uri.TryCreate($"https://candidate.local{(hint.StartsWith('/') ? string.Empty : "/")}{hint}", UriKind.Absolute, out uri))
            {
                continue;
            }

            var path = uri.AbsolutePath;
            if (path.Contains("/category/", StringComparison.OrdinalIgnoreCase))
            {
                patterns.Add("/category/");
                continue;
            }

            if (path.Contains("/department/", StringComparison.OrdinalIgnoreCase))
            {
                patterns.Add("/department/");
                continue;
            }

            var firstSegment = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstSegment))
            {
                patterns.Add($"/{firstSegment}/");
            }
        }

        return patterns.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<string> InferProductUrlPatterns(string? homePageHtml)
    {
        if (string.IsNullOrWhiteSpace(homePageHtml))
        {
            return [];
        }

        var patterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var href in ExtractHrefs(homePageHtml))
        {
            if (href.Contains("/product/", StringComparison.OrdinalIgnoreCase))
            {
                patterns.Add("/product/");
            }

            if (href.Contains("/products/", StringComparison.OrdinalIgnoreCase))
            {
                patterns.Add("/products/");
            }

            if (href.Contains("/p/", StringComparison.OrdinalIgnoreCase))
            {
                patterns.Add("/p/");
            }

            if (href.Contains("/dp/", StringComparison.OrdinalIgnoreCase))
            {
                patterns.Add("/dp/");
            }

            if (href.Contains("/item/", StringComparison.OrdinalIgnoreCase))
            {
                patterns.Add("/item/");
            }
        }

        return patterns.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static decimal ScoreCategoryRelevance(IReadOnlyCollection<string> categoryKeys, string? homePageHtml, IEnumerable<string> categoryPageHints)
    {
        if (categoryKeys.Count == 0)
        {
            return 0m;
        }

        var score = 0m;
        var html = homePageHtml ?? string.Empty;
        foreach (var categoryKey in categoryKeys.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (html.Contains(categoryKey, StringComparison.OrdinalIgnoreCase))
            {
                score += 6m;
            }
        }

        var hintCount = categoryPageHints.Count();
        score += Math.Min(22m, hintCount * 6m);

        return Math.Min(40m, score);
    }

    private static IReadOnlyList<string> ExtractHrefs(string html)
    {
        return HrefRegex().Matches(html)
            .Select(match => match.Groups["href"].Value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    [GeneratedRegex("^\\s*Sitemap:\\s*(?<url>\\S+)\\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex SitemapDirectiveRegex();

    [GeneratedRegex("href\\s*=\\s*[\"'](?<href>[^\"'#>]+)[\"']", RegexOptions.IgnoreCase)]
    private static partial Regex HrefRegex();
}