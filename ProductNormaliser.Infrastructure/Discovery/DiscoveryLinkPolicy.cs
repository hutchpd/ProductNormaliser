using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Discovery;

public sealed class DiscoveryLinkPolicy
{
    private static readonly HashSet<string> TrackingParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "fbclid",
        "gclid",
        "mc_cid",
        "mc_eid",
        "msclkid",
        "ref",
        "ref_src",
        "referrer",
        "source",
        "utm_campaign",
        "utm_content",
        "utm_id",
        "utm_medium",
        "utm_source",
        "utm_term"
    };

    public string NormalizeUrl(string url)
    {
        var uri = new Uri(url, UriKind.Absolute);
        var authority = uri.IsDefaultPort
            ? $"{uri.Scheme.ToLowerInvariant()}://{uri.Host.ToLowerInvariant()}"
            : $"{uri.Scheme.ToLowerInvariant()}://{uri.Host.ToLowerInvariant()}:{uri.Port}";

        var path = string.IsNullOrWhiteSpace(uri.AbsolutePath)
            ? "/"
            : uri.AbsolutePath;

        if (path.Length > 1)
        {
            path = path.TrimEnd('/');
        }

        var query = BuildNormalisedQuery(uri.Query);
        return string.IsNullOrWhiteSpace(query)
            ? $"{authority}{path}"
            : $"{authority}{path}?{query}";
    }

    public bool TryNormalizeAndValidate(CrawlSource source, string categoryKey, string candidateUrl, int depth, out string normalizedUrl)
    {
        normalizedUrl = string.Empty;
        if (!Uri.TryCreate(candidateUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        normalizedUrl = NormalizeUrl(uri.ToString());
        return IsAllowed(source, categoryKey, normalizedUrl, depth);
    }

    public bool IsAllowed(CrawlSource source, string categoryKey, string url, int depth)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return IsAllowed(source, categoryKey, uri, depth);
    }

    public bool MatchesProductPattern(CrawlSource source, string url)
    {
        return source.DiscoveryProfile.ProductUrlPatterns.Any(pattern =>
            !string.IsNullOrWhiteSpace(pattern)
            && url.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    public bool MatchesListingPattern(CrawlSource source, string url)
    {
        return source.DiscoveryProfile.ListingUrlPatterns.Any(pattern =>
            !string.IsNullOrWhiteSpace(pattern)
            && url.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    public bool LooksLikeSitemap(string urlOrPath)
    {
        var path = urlOrPath;
        if (Uri.TryCreate(urlOrPath, UriKind.Absolute, out var uri))
        {
            path = uri.AbsolutePath;
        }

        return path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            && path.Contains("sitemap", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsAllowed(CrawlSource source, string categoryKey, Uri uri, int depth)
    {
        if ((uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || depth > source.DiscoveryProfile.MaxDiscoveryDepth
            || !HostMatches(source, uri.Host))
        {
            return false;
        }

        var path = uri.AbsolutePath;
        var profile = source.DiscoveryProfile;

        if (profile.ExcludedPathPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (LooksLikeSitemap(path)
            || MatchesAllowedPrefix(profile, path)
            || MatchesCategoryEntryPrefix(profile, categoryKey, path)
            || MatchesProductPattern(source, uri.ToString())
            || MatchesListingPattern(source, uri.ToString()))
        {
            return true;
        }

        var hasCategoryRules = profile.CategoryEntryPages.TryGetValue(categoryKey, out var entryPagesWithRules)
            && entryPagesWithRules.Count > 0;

        return profile.AllowedPathPrefixes.Count == 0 && !hasCategoryRules;
    }

    private static bool MatchesAllowedPrefix(SourceDiscoveryProfile profile, string path)
    {
        return profile.AllowedPathPrefixes.Count == 0
            || profile.AllowedPathPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesCategoryEntryPrefix(SourceDiscoveryProfile profile, string categoryKey, string path)
    {
        if (!profile.CategoryEntryPages.TryGetValue(categoryKey, out var entryPages))
        {
            return false;
        }

        foreach (var entryPage in entryPages)
        {
            if (!Uri.TryCreate(entryPage, UriKind.Absolute, out var uri))
            {
                continue;
            }

            var entryPath = uri.AbsolutePath.TrimEnd('/');
            if (entryPath.Length == 0)
            {
                continue;
            }

            if (path.StartsWith(entryPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HostMatches(CrawlSource source, string candidateHost)
    {
        var expectedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NormaliseHost(source.Host)
        };

        if (Uri.TryCreate(source.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            expectedHosts.Add(NormaliseHost(baseUri.Host));
        }

        return expectedHosts.Contains(NormaliseHost(candidateHost));
    }

    private static string NormaliseHost(string host)
    {
        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? host[4..]
            : host;
    }

    private static string BuildNormalisedQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query == "?")
        {
            return string.Empty;
        }

        var parameters = query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseQueryParameter)
            .Where(pair => pair.Key.Length > 0 && !TrackingParameters.Contains(pair.Key))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(pair => pair.Value, StringComparer.Ordinal)
            .ToArray();

        return string.Join("&", parameters.Select(parameter =>
            parameter.Value.Length == 0
                ? Uri.EscapeDataString(parameter.Key)
                : $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}"));
    }

    private static KeyValuePair<string, string> ParseQueryParameter(string segment)
    {
        var parts = segment.Split('=', 2);
        var key = Uri.UnescapeDataString(parts[0]);
        var value = parts.Length == 2
            ? Uri.UnescapeDataString(parts[1])
            : string.Empty;

        return new KeyValuePair<string, string>(key, value);
    }
}