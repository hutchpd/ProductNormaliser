using System.Security.Cryptography;
using System.Text;

namespace ProductNormaliser.Infrastructure.Discovery;

internal static class DiscoveryIdentity
{
    public static string BuildDiscoveryQueueId(string sourceId, string categoryKey, string url, string itemType)
    {
        return $"discovery:{itemType}:{sourceId}:{categoryKey}:{Hash(NormalizeUrl(url))}";
    }

    public static string BuildDiscoveredUrlId(string sourceId, string categoryKey, string url)
    {
        return $"discovered:{sourceId}:{categoryKey}:{Hash(NormalizeUrl(url))}";
    }

    public static string BuildCrawlQueueId(string sourceId, string categoryKey, string url)
    {
        return $"crawl:discovered:{sourceId}:{categoryKey}:{Hash(NormalizeUrl(url))}";
    }

    public static string NormalizeUrl(string url)
    {
        var uri = new Uri(url.Trim(), UriKind.Absolute);
        var builder = new UriBuilder(uri)
        {
            Fragment = string.Empty,
            Query = string.Empty
        };

        return builder.Uri.ToString().TrimEnd('/');
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}