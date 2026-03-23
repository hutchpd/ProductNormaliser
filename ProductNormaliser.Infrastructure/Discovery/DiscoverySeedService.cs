using Microsoft.Extensions.Logging;
using ProductNormaliser.Application.Sources;

namespace ProductNormaliser.Infrastructure.Discovery;

public sealed class DiscoverySeedService(
    ICrawlSourceStore crawlSourceStore,
    IDiscoveryQueueService discoveryQueueService,
    SitemapLocator sitemapLocator,
    ILogger<DiscoverySeedService> logger)
{
    public async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        var sources = await crawlSourceStore.ListAsync(cancellationToken);
        foreach (var source in sources.Where(source => source.IsEnabled))
        {
            var sitemapUrls = await sitemapLocator.LocateAsync(source, cancellationToken);
            foreach (var categoryKey in source.SupportedCategoryKeys)
            {
                if (source.DiscoveryProfile.CategoryEntryPages.TryGetValue(categoryKey, out var entryPages))
                {
                    foreach (var entryPage in entryPages)
                    {
                        await discoveryQueueService.EnqueueAsync(source, categoryKey, entryPage, "listing", depth: 0, parentUrl: null, cancellationToken);
                    }
                }

                foreach (var sitemapUrl in sitemapUrls)
                {
                    await discoveryQueueService.EnqueueAsync(source, categoryKey, sitemapUrl, "sitemap", depth: 0, parentUrl: null, cancellationToken);
                }
            }
        }

        logger.LogDebug("Ensured discovery seeds for {SourceCount} enabled sources.", sources.Count(source => source.IsEnabled));
    }
}