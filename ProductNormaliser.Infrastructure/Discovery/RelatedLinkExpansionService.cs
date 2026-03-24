using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.RegularExpressions;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Discovery;

public interface IRelatedLinkExpansionService
{
    Task<RelatedLinkExpansionResult> ExpandAsync(CrawlTarget target, string html, CancellationToken cancellationToken);
}

public sealed partial class RelatedLinkExpansionService(
    ICrawlSourceStore crawlSourceStore,
    ProductLinkExtractor productLinkExtractor,
    ProductPageClassifier productPageClassifier,
    ListingPageClassifier listingPageClassifier,
    DiscoveryLinkPolicy discoveryLinkPolicy,
    IDiscoveryQueueService discoveryQueueService,
    ILogger<RelatedLinkExpansionService> logger) : IRelatedLinkExpansionService
{
    public async Task<RelatedLinkExpansionResult> ExpandAsync(CrawlTarget target, string html, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (string.IsNullOrWhiteSpace(html)
            || !target.Metadata.TryGetValue("sourceName", out var sourceName)
            || string.IsNullOrWhiteSpace(sourceName))
        {
            return RelatedLinkExpansionResult.Empty;
        }

        var source = await crawlSourceStore.GetAsync(sourceName.Trim(), cancellationToken);
        if (source is null || !source.IsEnabled)
        {
            return RelatedLinkExpansionResult.Empty;
        }

        const int childDepth = 1;
        var links = productLinkExtractor.Extract(source, target.CategoryKey, html, target.Url, childDepth);
        var breadcrumbLinks = ExtractBreadcrumbLinks(source, target.CategoryKey, html, target.Url, childDepth).ToArray();

        var productCandidates = links.ProductLinks
            .Concat(links.RelatedLinks.Where(url => productPageClassifier.IsLikelyProductUrl(source, url)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(source.DiscoveryProfile.MaxUrlsPerRun)
            .ToArray();

        var listingCandidates = links.PaginationLinks
            .Concat(links.CategoryLinks)
            .Concat(links.RelatedLinks.Where(url => listingPageClassifier.IsLikelyListingUrl(source, url)))
            .Concat(breadcrumbLinks)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(source.DiscoveryProfile.MaxUrlsPerRun)
            .ToArray();

        var enqueuedProductUrls = 0;
        foreach (var productUrl in productCandidates)
        {
            enqueuedProductUrls += await discoveryQueueService.EnqueueAsync(source, target.CategoryKey, productUrl, "product", childDepth, target.Url, jobId: null, cancellationToken) ? 1 : 0;
        }

        var enqueuedListingUrls = 0;
        foreach (var listingUrl in listingCandidates)
        {
            enqueuedListingUrls += await discoveryQueueService.EnqueueAsync(source, target.CategoryKey, listingUrl, "listing", childDepth, target.Url, jobId: null, cancellationToken) ? 1 : 0;
        }

        if (enqueuedProductUrls > 0 || enqueuedListingUrls > 0)
        {
            logger.LogDebug(
                "Expanded related links for {Url}; enqueuedProducts={EnqueuedProducts}, enqueuedListings={EnqueuedListings}",
                target.Url,
                enqueuedProductUrls,
                enqueuedListingUrls);
        }

        return new RelatedLinkExpansionResult(enqueuedProductUrls, enqueuedListingUrls, breadcrumbLinks.Length);
    }

    private IEnumerable<string> ExtractBreadcrumbLinks(CrawlSource source, string categoryKey, string html, string baseUrl, int depth)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            yield break;
        }

        var baseUri = new Uri(baseUrl, UriKind.Absolute);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match container in BreadcrumbContainerRegex().Matches(html))
        {
            foreach (Match anchor in AnchorRegex().Matches(container.Value))
            {
                var href = WebUtility.HtmlDecode(anchor.Groups["href"].Value).Trim();
                if (href.Length == 0 || !Uri.TryCreate(baseUri, href, out var absoluteUri))
                {
                    continue;
                }

                if (!discoveryLinkPolicy.TryNormalizeAndValidate(source, categoryKey, absoluteUri.ToString(), depth, out var normalized)
                    || productPageClassifier.IsLikelyProductUrl(source, normalized)
                    || !seen.Add(normalized))
                {
                    continue;
                }

                yield return normalized;
            }
        }
    }

    [GeneratedRegex("<(nav|ol|ul|div)[^>]*(aria-label=[\"'][^\"']*breadcrumb[^\"']*[\"']|class=[\"'][^\"']*breadcrumb[^\"']*[\"'])[^>]*>.*?</(nav|ol|ul|div)>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex BreadcrumbContainerRegex();

    [GeneratedRegex("<a\\b(?<attrs>[^>]*)href=[\"'](?<href>[^\"'#>]+)[\"'][^>]*>(?<text>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex AnchorRegex();
}

public sealed record RelatedLinkExpansionResult(int EnqueuedProductUrls, int EnqueuedListingUrls, int BreadcrumbLinks)
{
    public static RelatedLinkExpansionResult Empty { get; } = new(0, 0, 0);

    public int TotalEnqueued => EnqueuedProductUrls + EnqueuedListingUrls;
}
