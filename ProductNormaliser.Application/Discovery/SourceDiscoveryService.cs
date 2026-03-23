using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Discovery;

public sealed class SourceDiscoveryService(
    ICrawlSourceStore crawlSourceStore,
    ISitemapLocator sitemapLocator,
    IDiscoverySeedWriter discoverySeedWriter,
    ILogger<SourceDiscoveryService>? logger = null)
{
    private readonly ILogger<SourceDiscoveryService> logger = logger ?? NullLogger<SourceDiscoveryService>.Instance;

    public Task<SourceDiscoverySeedResult> EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        return SeedAsync([], [], jobId: null, cancellationToken);
    }

    public async Task<SourceDiscoverySeedResult> SeedAsync(
        IReadOnlyCollection<string>? categoryKeys,
        IReadOnlyCollection<string>? sourceIds,
        string? jobId,
        CancellationToken cancellationToken = default)
    {
        var normalizedCategories = NormalizeValues(categoryKeys);
        var normalizedSourceIds = NormalizeValues(sourceIds);

        var sources = await crawlSourceStore.ListAsync(cancellationToken);
        var selectedSources = sources
            .Where(source => source.IsEnabled)
            .Where(source => normalizedSourceIds.Count == 0 || normalizedSourceIds.Contains(source.Id))
            .Select(source => new
            {
                Source = source,
                Categories = ResolveCategories(source, normalizedCategories)
            })
            .Where(item => item.Categories.Count > 0)
            .ToArray();

        var result = new SourceDiscoverySeedResult
        {
            SourceCount = selectedSources.Length,
            CategoryCount = selectedSources.Sum(item => item.Categories.Count)
        };

        foreach (var selection in selectedSources)
        {
            var sitemapUrls = await sitemapLocator.LocateAsync(selection.Source, cancellationToken);
            foreach (var categoryKey in selection.Categories)
            {
                if (selection.Source.DiscoveryProfile.CategoryEntryPages.TryGetValue(categoryKey, out var entryPages))
                {
                    foreach (var entryPage in entryPages)
                    {
                        if (await discoverySeedWriter.EnqueueAsync(selection.Source, categoryKey, entryPage, "listing", depth: 0, parentUrl: null, jobId, cancellationToken))
                        {
                            result.SeedCount += 1;
                        }
                    }
                }

                foreach (var sitemapUrl in sitemapUrls)
                {
                    if (await discoverySeedWriter.EnqueueAsync(selection.Source, categoryKey, sitemapUrl, "sitemap", depth: 0, parentUrl: null, jobId, cancellationToken))
                    {
                        result.SeedCount += 1;
                    }
                }
            }
        }

        logger.LogInformation(
            "Seeded discovery for {SourceCount} source(s), {CategoryCount} source/category pair(s), enqueued {SeedCount} initial seed(s)",
            result.SourceCount,
            result.CategoryCount,
            result.SeedCount);

        return result;
    }

    private static HashSet<string> NormalizeValues(IReadOnlyCollection<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return [];
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ResolveCategories(CrawlSource source, HashSet<string> requestedCategories)
    {
        return source.SupportedCategoryKeys
            .Where(categoryKey => requestedCategories.Count == 0 || requestedCategories.Contains(categoryKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(categoryKey => categoryKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public sealed class SourceDiscoverySeedResult
{
    public int SourceCount { get; init; }
    public int CategoryCount { get; init; }
    public int SeedCount { get; set; }
}