using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Discovery;

public sealed class SourceDiscoveryService(
    ICrawlSourceStore crawlSourceStore,
    ISitemapLocator sitemapLocator,
    IDiscoverySeedWriter discoverySeedWriter,
    IDiscoveryLinkPolicy discoveryLinkPolicy,
    ILogger<SourceDiscoveryService>? logger = null) : ISourceDiscoveryService
{
    private readonly ILogger<SourceDiscoveryService> logger = logger ?? NullLogger<SourceDiscoveryService>.Instance;

    public Task<SourceDiscoverySeedResult> EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        return SeedAsync([], [], jobId: null, cancellationToken);
    }

    public async Task<SourceDiscoveryPreview> PreviewAsync(
        IReadOnlyCollection<string>? categoryKeys,
        IReadOnlyCollection<string>? sourceIds,
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

        var seeds = new List<SourceDiscoverySeedDescriptor>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var selection in selectedSources)
        {
            var sitemapUrls = await sitemapLocator.LocateAsync(selection.Source, cancellationToken);
            foreach (var categoryKey in selection.Categories)
            {
                if (selection.Source.DiscoveryProfile.CategoryEntryPages.TryGetValue(categoryKey, out var entryPages))
                {
                    foreach (var entryPage in entryPages)
                    {
                        AddSeed(selection.Source, categoryKey, entryPage, "listing");
                    }
                }

                foreach (var sitemapUrl in sitemapUrls)
                {
                    AddSeed(selection.Source, categoryKey, sitemapUrl, "sitemap");
                }
            }
        }

        return new SourceDiscoveryPreview
        {
            SourceCount = selectedSources.Length,
            CategoryCount = selectedSources.Sum(item => item.Categories.Count),
            Seeds = seeds
        };

        void AddSeed(CrawlSource source, string categoryKey, string url, string classification)
        {
            if (!discoveryLinkPolicy.TryNormalizeAndValidate(source, categoryKey, url, depth: 0, out var normalizedUrl))
            {
                return;
            }

            var key = $"{source.Id}|{categoryKey}|{classification}|{normalizedUrl}";
            if (!seen.Add(key))
            {
                return;
            }

            seeds.Add(new SourceDiscoverySeedDescriptor
            {
                SourceId = source.Id,
                CategoryKey = categoryKey,
                Url = normalizedUrl,
                Classification = classification
            });
        }
    }

    public async Task<SourceDiscoverySeedResult> SeedAsync(
        IReadOnlyCollection<string>? categoryKeys,
        IReadOnlyCollection<string>? sourceIds,
        string? jobId,
        CancellationToken cancellationToken = default)
    {
        var preview = await PreviewAsync(categoryKeys, sourceIds, cancellationToken);
        var sources = await crawlSourceStore.ListAsync(cancellationToken);
        var sourceMap = sources.ToDictionary(source => source.Id, StringComparer.OrdinalIgnoreCase);

        var result = new SourceDiscoverySeedResult
        {
            SourceCount = preview.SourceCount,
            CategoryCount = preview.CategoryCount
        };

        foreach (var seed in preview.Seeds)
        {
            if (!sourceMap.TryGetValue(seed.SourceId, out var source))
            {
                continue;
            }

            if (await discoverySeedWriter.EnqueueAsync(source, seed.CategoryKey, seed.Url, seed.Classification, depth: 0, parentUrl: null, jobId, cancellationToken))
            {
                result.SeedCount += 1;
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

public sealed class SourceDiscoveryPreview
{
    public int SourceCount { get; init; }
    public int CategoryCount { get; init; }
    public IReadOnlyList<SourceDiscoverySeedDescriptor> Seeds { get; init; } = [];
}

public sealed class SourceDiscoverySeedResult
{
    public int SourceCount { get; init; }
    public int CategoryCount { get; init; }
    public int SeedCount { get; set; }
}

public sealed class SourceDiscoverySeedDescriptor
{
    public string SourceId { get; init; } = string.Empty;
    public string CategoryKey { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Classification { get; init; } = string.Empty;
}