using System.Collections.Concurrent;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Crawling;

public sealed class RobotsTxtCache(IHttpFetcher httpFetcher) : IRobotsTxtCache
{
    private readonly ConcurrentDictionary<string, Lazy<Task<RobotsTxtSnapshot>>> cache = new(StringComparer.OrdinalIgnoreCase);

    public Task<RobotsTxtSnapshot> GetAsync(CrawlTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        var uri = new Uri(target.Url, UriKind.Absolute);
        var cacheKey = BuildCacheKey(uri);
        var sourceName = target.Metadata.TryGetValue("sourceName", out var metadataSourceName)
            ? metadataSourceName
            : null;

        var lazySnapshot = cache.GetOrAdd(
            cacheKey,
            _ => new Lazy<Task<RobotsTxtSnapshot>>(() => LoadSnapshotAsync(uri, sourceName, cancellationToken), LazyThreadSafetyMode.ExecutionAndPublication));

        return lazySnapshot.Value;
    }

    public Task<RobotsTxtSnapshot> GetForSourceAsync(CrawlSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        return GetAsync(new CrawlTarget
        {
            Url = new Uri(new Uri(source.BaseUrl, UriKind.Absolute), "/").ToString(),
            CategoryKey = source.SupportedCategoryKeys.FirstOrDefault() ?? string.Empty,
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceName"] = source.Id
            }
        }, cancellationToken);
    }

    private async Task<RobotsTxtSnapshot> LoadSnapshotAsync(Uri targetUri, string? sourceName, CancellationToken cancellationToken)
    {
        var robotsUri = new UriBuilder(targetUri.Scheme, targetUri.Host, targetUri.Port, "/robots.txt").Uri;
        var target = new CrawlTarget
        {
            Url = robotsUri.ToString(),
            CategoryKey = string.Empty,
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };

        if (!string.IsNullOrWhiteSpace(sourceName))
        {
            target.Metadata["sourceName"] = sourceName;
        }

        try
        {
            var fetchResult = await httpFetcher.FetchAsync(target, cancellationToken);
            if (!fetchResult.IsSuccess || string.IsNullOrWhiteSpace(fetchResult.Html))
            {
                return RobotsTxtSnapshot.AllowAll;
            }

            return RobotsTxtSnapshot.Parse(fetchResult.Html);
        }
        catch
        {
            return RobotsTxtSnapshot.AllowAll;
        }
    }

    private static string BuildCacheKey(Uri uri)
    {
        return uri.IsDefaultPort
            ? $"{uri.Scheme.ToLowerInvariant()}://{uri.Host.ToLowerInvariant()}"
            : $"{uri.Scheme.ToLowerInvariant()}://{uri.Host.ToLowerInvariant()}:{uri.Port}";
    }
}

public sealed class RobotsTxtSnapshot
{
    public static RobotsTxtSnapshot AllowAll { get; } = new([], [], []);

    private readonly IReadOnlyList<string> allows;
    private readonly IReadOnlyList<string> disallows;

    public RobotsTxtSnapshot(IReadOnlyList<string> allows, IReadOnlyList<string> disallows, IReadOnlyList<string> sitemapUrls)
    {
        this.allows = allows;
        this.disallows = disallows;
        SitemapUrls = sitemapUrls;
    }

    public IReadOnlyList<string> SitemapUrls { get; }

    public bool IsBlocked(string path)
    {
        var bestAllowLength = allows
            .Where(rule => path.StartsWith(rule, StringComparison.OrdinalIgnoreCase))
            .Select(rule => rule.Length)
            .DefaultIfEmpty(-1)
            .Max();

        var bestDisallowLength = disallows
            .Where(rule => rule.Length > 0 && path.StartsWith(rule, StringComparison.OrdinalIgnoreCase))
            .Select(rule => rule.Length)
            .DefaultIfEmpty(-1)
            .Max();

        return bestDisallowLength > bestAllowLength;
    }

    public static RobotsTxtSnapshot Parse(string robotsText)
    {
        var allows = new List<string>();
        var disallows = new List<string>();
        var sitemapUrls = new List<string>();
        var matchingAgent = false;

        foreach (var line in robotsText.Split('\n'))
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.Length == 0 || trimmedLine.StartsWith('#'))
            {
                continue;
            }

            var parts = trimmedLine.Split(':', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            var directive = parts[0].Trim().ToLowerInvariant();
            var value = parts[1].Trim();

            if (directive == "sitemap")
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    sitemapUrls.Add(value);
                }

                continue;
            }

            if (directive == "user-agent")
            {
                matchingAgent = value == "*";
                continue;
            }

            if (!matchingAgent)
            {
                continue;
            }

            if (directive == "allow")
            {
                allows.Add(value);
            }
            else if (directive == "disallow")
            {
                disallows.Add(value);
            }
        }

        return new RobotsTxtSnapshot(allows, disallows, sitemapUrls.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }
}