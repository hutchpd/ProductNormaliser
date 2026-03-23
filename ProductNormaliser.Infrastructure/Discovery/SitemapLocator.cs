using ProductNormaliser.Application.Discovery;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Crawling;

namespace ProductNormaliser.Infrastructure.Discovery;

public sealed class SitemapLocator(IRobotsTxtCache robotsTxtCache, DiscoveryLinkPolicy discoveryLinkPolicy) : ISitemapLocator
{
    public async Task<IReadOnlyList<string>> LocateAsync(CrawlSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        var baseUri = new Uri(source.BaseUrl, UriKind.Absolute);
        var sitemapUrls = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var robotsSnapshot = await robotsTxtCache.GetForSourceAsync(source, cancellationToken);
        foreach (var sitemapUrl in robotsSnapshot.SitemapUrls)
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
            if (!Uri.TryCreate(baseUri, candidate, out var sitemapUri))
            {
                return;
            }

            if (discoveryLinkPolicy.TryNormalizeAndValidate(source, string.Empty, sitemapUri.ToString(), depth: 0, out var normalized)
                && seen.Add(normalized))
            {
                sitemapUrls.Add(normalized);
            }
        }
    }
}