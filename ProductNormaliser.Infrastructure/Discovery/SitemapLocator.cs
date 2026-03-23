using ProductNormaliser.Application.Discovery;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Crawling;

namespace ProductNormaliser.Infrastructure.Discovery;

public sealed class SitemapLocator(IHttpFetcher httpFetcher, DiscoveryLinkPolicy discoveryLinkPolicy) : ISitemapLocator
{
    public async Task<IReadOnlyList<string>> LocateAsync(CrawlSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        var baseUri = new Uri(source.BaseUrl, UriKind.Absolute);
        var sitemapUrls = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sitemapUrl in await LoadRobotsSitemapsAsync(source, baseUri, cancellationToken))
        {
            AddSitemap(sitemapUrl);
        }

        AddSitemap(new Uri(baseUri, "/sitemap.xml").ToString());
        AddSitemap(new Uri(baseUri, "/sitemap_index.xml").ToString());

        foreach (var sitemapHint in source.DiscoveryProfile.SitemapHints)
        {
            if (string.IsNullOrWhiteSpace(sitemapHint))
            {
                continue;
            }

            if (!Uri.TryCreate(baseUri, sitemapHint, out var resolvedHint))
            {
                continue;
            }

            AddSitemap(resolvedHint.ToString());
        }

        return sitemapUrls;

        void AddSitemap(string candidate)
        {
            if (discoveryLinkPolicy.TryNormalizeAndValidate(source, string.Empty, candidate, depth: 0, out var normalized)
                && seen.Add(normalized))
            {
                sitemapUrls.Add(normalized);
            }
        }
    }

    private async Task<IReadOnlyList<string>> LoadRobotsSitemapsAsync(CrawlSource source, Uri baseUri, CancellationToken cancellationToken)
    {
        var robotsUrl = new Uri(baseUri, "/robots.txt").ToString();
        var fetchResult = await httpFetcher.FetchAsync(new CrawlTarget
        {
            Url = robotsUrl,
            CategoryKey = string.Empty,
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceName"] = source.Id
            }
        }, cancellationToken);

        if (!fetchResult.IsSuccess || string.IsNullOrWhiteSpace(fetchResult.Html))
        {
            return [];
        }

        var locations = new List<string>();
        foreach (var line in fetchResult.Html.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!line.StartsWith("Sitemap:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = line[8..].Trim();
            if (value.Length == 0 || !Uri.TryCreate(baseUri, value, out var sitemapUri))
            {
                continue;
            }

            locations.Add(sitemapUri.ToString());
        }

        return locations;
    }
}