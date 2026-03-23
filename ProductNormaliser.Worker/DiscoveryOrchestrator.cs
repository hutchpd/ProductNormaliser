using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Crawling;
using ProductNormaliser.Infrastructure.Discovery;

namespace ProductNormaliser.Worker;

public sealed class DiscoveryOrchestrator(
    ICrawlSourceStore crawlSourceStore,
    IRobotsPolicyService robotsPolicyService,
    IHttpFetcher httpFetcher,
    SitemapParser sitemapParser,
    DiscoveryLinkPolicy discoveryLinkPolicy,
    ProductPageClassifier productPageClassifier,
    ListingPageClassifier listingPageClassifier,
    IDiscoveryQueueService discoveryQueueService,
    ILogger<DiscoveryOrchestrator> logger)
{
    public async Task<DiscoveryProcessResult> ProcessAsync(DiscoveryQueueItem item, CancellationToken cancellationToken)
    {
        var source = await crawlSourceStore.GetAsync(item.SourceId, cancellationToken);
        if (source is null || !source.IsEnabled)
        {
            return DiscoveryProcessResult.Skipped($"Source '{item.SourceId}' is not available for discovery.");
        }

        var target = new CrawlTarget
        {
            Url = item.Url,
            CategoryKey = item.CategoryKey,
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceName"] = source.Id
            }
        };

        var robotsDecision = await robotsPolicyService.EvaluateAsync(target, cancellationToken);
        if (!robotsDecision.IsAllowed)
        {
            return DiscoveryProcessResult.Skipped(robotsDecision.Reason);
        }

        var fetchResult = await httpFetcher.FetchAsync(target, cancellationToken);
        if (!fetchResult.IsSuccess || string.IsNullOrWhiteSpace(fetchResult.Html))
        {
            return DiscoveryProcessResult.Failed(fetchResult.FailureReason ?? "Discovery fetch failed.");
        }

        return item.Classification switch
        {
            "sitemap" => await ProcessSitemapAsync(source, item, fetchResult.Html, cancellationToken),
            _ => await ProcessListingAsync(source, item, fetchResult.Html, cancellationToken)
        };
    }

    private async Task<DiscoveryProcessResult> ProcessSitemapAsync(CrawlSource source, DiscoveryQueueItem item, string xml, CancellationToken cancellationToken)
    {
        SitemapParseResult parsed;
        try
        {
            parsed = sitemapParser.Parse(xml);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to parse sitemap {Url}", item.Url);
            return DiscoveryProcessResult.Failed("Invalid sitemap XML.");
        }

        var enqueuedCount = 0;
        foreach (var sitemapUrl in parsed.ChildSitemaps)
        {
            if (item.Depth + 1 > source.DiscoveryProfile.MaxDiscoveryDepth)
            {
                break;
            }

            if (discoveryLinkPolicy.IsAllowed(source, item.CategoryKey, sitemapUrl, item.Depth + 1)
                && await discoveryQueueService.EnqueueAsync(source, item.CategoryKey, sitemapUrl, "sitemap", item.Depth + 1, item.Url, item.JobId, cancellationToken))
            {
                enqueuedCount += 1;
            }
        }

        foreach (var pageUrl in parsed.CandidateUrls.Take(source.DiscoveryProfile.MaxUrlsPerRun))
        {
            enqueuedCount += await RouteSitemapCandidateAsync(source, item.CategoryKey, pageUrl, item.Depth + 1, item.Url, cancellationToken) ? 1 : 0;
        }

        return DiscoveryProcessResult.Completed($"Processed sitemap and routed {enqueuedCount} URL(s).");
    }

    private async Task<DiscoveryProcessResult> ProcessListingAsync(CrawlSource source, DiscoveryQueueItem item, string html, CancellationToken cancellationToken)
    {
        var productClassification = productPageClassifier.Classify(source, item.Url, html);
        if (productClassification.IsProductPage)
        {
            var promoted = await discoveryQueueService.EnqueueProductAsync(source, item.CategoryKey, item.Url, item.Depth, item.ParentUrl, item.JobId, cancellationToken);
            var message = promoted
                ? $"Current page classified as product. {productClassification.Reason}"
                : $"Current page matched product classification but was already queued. {productClassification.Reason}";
            return DiscoveryProcessResult.Completed(message);
        }

        var listingClassification = listingPageClassifier.Classify(source, item.CategoryKey, item.Url, html, item.Depth + 1);
        if (!listingClassification.IsListingPage)
        {
            return DiscoveryProcessResult.Skipped(listingClassification.Reason);
        }

        var enqueuedCount = 0;
        foreach (var productLink in listingClassification.Links.ProductLinks.Take(source.DiscoveryProfile.MaxUrlsPerRun))
        {
            enqueuedCount += await discoveryQueueService.EnqueueProductAsync(source, item.CategoryKey, productLink, item.Depth + 1, item.Url, item.JobId, cancellationToken) ? 1 : 0;
        }

        var listingLinks = listingClassification.Links.PaginationLinks
            .Concat(listingClassification.Links.CategoryLinks)
            .Concat(listingClassification.Links.RelatedLinks)
            .Take(source.DiscoveryProfile.MaxUrlsPerRun)
            .ToArray();

        foreach (var link in listingLinks)
        {
            enqueuedCount += await discoveryQueueService.EnqueueAsync(source, item.CategoryKey, link, "listing", item.Depth + 1, item.Url, item.JobId, cancellationToken) ? 1 : 0;
        }

        return DiscoveryProcessResult.Completed($"Processed listing page and routed {enqueuedCount} URL(s).");
    }

    private async Task<bool> RouteSitemapCandidateAsync(CrawlSource source, string categoryKey, string url, int depth, string parentUrl, CancellationToken cancellationToken)
    {
        if (!discoveryLinkPolicy.IsAllowed(source, categoryKey, url, depth))
        {
            return false;
        }

        if (discoveryLinkPolicy.LooksLikeSitemap(url) && depth <= source.DiscoveryProfile.MaxDiscoveryDepth)
        {
            return await discoveryQueueService.EnqueueAsync(source, categoryKey, url, "sitemap", depth, parentUrl, null, cancellationToken);
        }

        if (productPageClassifier.IsLikelyProductUrl(source, url))
        {
            return await discoveryQueueService.EnqueueProductAsync(source, categoryKey, url, depth, parentUrl, null, cancellationToken);
        }

        if (listingPageClassifier.IsLikelyListingUrl(source, url) && depth <= source.DiscoveryProfile.MaxDiscoveryDepth)
        {
            return await discoveryQueueService.EnqueueAsync(source, categoryKey, url, "listing", depth, parentUrl, null, cancellationToken);
        }

        return false;
    }
}