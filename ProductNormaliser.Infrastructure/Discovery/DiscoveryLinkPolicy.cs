using System.Text.RegularExpressions;
using ProductNormaliser.Application.Discovery;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Discovery;

public sealed partial class DiscoveryLinkPolicy : IDiscoveryLinkPolicy
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

    private static readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> MarketAliases = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
    {
        ["UK"] = ["uk", "gb"],
        ["GB"] = ["gb", "uk"],
        ["US"] = ["us"],
        ["IE"] = ["ie"],
        ["EU"] = ["eu"]
    };

    private static readonly IReadOnlySet<string> KnownBoundaryMarketTokens = BuildKnownBoundaryMarketTokens();

    private static readonly IReadOnlySet<string> NeutralBoundaryTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "global",
        "intl",
        "international",
        "row"
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

        if (HasExplicitRuntimeOverride(source, categoryKey, uri))
        {
            return true;
        }

        if (StronglyContradictsMarketOrLocale(source, uri))
        {
            return false;
        }

        if (LooksLikeSitemap(path)
            || MatchesProductPattern(source, uri.ToString())
            || MatchesListingPattern(source, uri.ToString()))
        {
            return true;
        }

        var hasCategoryRules = profile.CategoryEntryPages.TryGetValue(categoryKey, out var entryPagesWithRules)
            && entryPagesWithRules.Count > 0;

        return profile.AllowedPathPrefixes.Count == 0 && !hasCategoryRules;
    }

    private static bool HasExplicitRuntimeOverride(CrawlSource source, string categoryKey, Uri uri)
    {
        var profile = source.DiscoveryProfile;
        var path = uri.AbsolutePath;

        return MatchesConfiguredAllowedHost(profile, uri.Host)
            || MatchesConfiguredAllowedPrefix(profile, path)
            || MatchesCategoryEntryPrefix(profile, categoryKey, path)
            || MatchesSitemapHint(source, uri);
    }

    private static bool MatchesConfiguredAllowedPrefix(SourceDiscoveryProfile profile, string path)
    {
        return profile.AllowedPathPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
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
        return source.GetDiscoveryHosts().Contains(NormaliseHost(candidateHost), StringComparer.OrdinalIgnoreCase);
    }

    private static bool MatchesConfiguredAllowedHost(SourceDiscoveryProfile profile, string candidateHost)
    {
        var normalizedHost = NormaliseHost(candidateHost);
        return profile.AllowedHosts.Any(allowedHost => string.Equals(NormaliseHost(allowedHost), normalizedHost, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesSitemapHint(CrawlSource source, Uri candidateUri)
    {
        foreach (var sitemapHint in source.DiscoveryProfile.SitemapHints)
        {
            if (!Uri.TryCreate(new Uri(source.BaseUrl, UriKind.Absolute), sitemapHint, out var absoluteHint))
            {
                continue;
            }

            if (string.Equals(absoluteHint.GetLeftPart(UriPartial.Path).TrimEnd('/'), candidateUri.GetLeftPart(UriPartial.Path).TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool StronglyContradictsMarketOrLocale(CrawlSource source, Uri uri)
    {
        var allowedMarketTokens = GetAllowedMarketTokens(source);
        var preferredLocale = GetPreferredLocale(source);

        foreach (var localeSignal in ExtractLocaleSignals(uri))
        {
            if (!string.IsNullOrWhiteSpace(preferredLocale)
                && !string.Equals(localeSignal, preferredLocale, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (allowedMarketTokens.Count == 0)
        {
            return false;
        }

        foreach (var marketSignal in ExtractMarketSignals(uri))
        {
            if (!NeutralBoundaryTokens.Contains(marketSignal)
                && !allowedMarketTokens.Contains(marketSignal))
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> GetAllowedMarketTokens(CrawlSource source)
    {
        var effectiveMarkets = source.DiscoveryProfile.AllowedMarkets.Count > 0
            ? source.DiscoveryProfile.AllowedMarkets
            : source.AllowedMarkets;
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var market in effectiveMarkets)
        {
            if (string.IsNullOrWhiteSpace(market))
            {
                continue;
            }

            var normalizedMarket = market.Trim().ToLowerInvariant();
            tokens.Add(normalizedMarket);
            if (MarketAliases.TryGetValue(market.Trim(), out var aliases))
            {
                foreach (var alias in aliases)
                {
                    tokens.Add(alias);
                }
            }
        }

        var preferredLocale = GetPreferredLocale(source);
        if (!string.IsNullOrWhiteSpace(preferredLocale))
        {
            tokens.Add(preferredLocale);
            var localeParts = preferredLocale.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (localeParts.Length == 2)
            {
                tokens.Add(localeParts[1].ToLowerInvariant());
                if (MarketAliases.TryGetValue(localeParts[1], out var aliases))
                {
                    foreach (var alias in aliases)
                    {
                        tokens.Add(alias);
                    }
                }
            }
        }

        return tokens;
    }

    private static string? GetPreferredLocale(CrawlSource source)
    {
        var preferredLocale = string.IsNullOrWhiteSpace(source.DiscoveryProfile.PreferredLocale)
            ? source.PreferredLocale
            : source.DiscoveryProfile.PreferredLocale;
        return NormalizeLocaleToken(preferredLocale);
    }

    private static IReadOnlyList<string> ExtractLocaleSignals(Uri uri)
    {
        var signals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var token in EnumerateBoundaryTokens(uri))
        {
            var locale = NormalizeLocaleToken(token);
            if (!string.IsNullOrWhiteSpace(locale))
            {
                signals.Add(locale);
            }
        }

        return signals.ToArray();
    }

    private static IReadOnlyList<string> ExtractMarketSignals(Uri uri)
    {
        var signals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var token in EnumerateBoundaryTokens(uri))
        {
            var locale = NormalizeLocaleToken(token);
            if (!string.IsNullOrWhiteSpace(locale))
            {
                var localeParts = locale.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (localeParts.Length == 2)
                {
                    signals.Add(localeParts[1]);
                }

                continue;
            }

            var normalized = token.Trim().ToLowerInvariant();
            if (KnownBoundaryMarketTokens.Contains(normalized))
            {
                signals.Add(normalized);
            }
        }

        return signals.ToArray();
    }

    private static IEnumerable<string> EnumerateBoundaryTokens(Uri uri)
    {
        foreach (var label in uri.Host.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (label.Length > 0)
            {
                yield return label;
            }
        }

        foreach (var segment in uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (segment.Length > 0)
            {
                yield return segment;
            }
        }

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=', 2, StringSplitOptions.TrimEntries);
            var key = parts[0];
            if (!IsBoundaryQueryParameter(key))
            {
                continue;
            }

            if (parts.Length == 2 && parts[1].Length > 0)
            {
                yield return Uri.UnescapeDataString(parts[1]);
            }
        }
    }

    private static bool IsBoundaryQueryParameter(string key)
    {
        return key.Equals("lang", StringComparison.OrdinalIgnoreCase)
            || key.Equals("language", StringComparison.OrdinalIgnoreCase)
            || key.Equals("locale", StringComparison.OrdinalIgnoreCase)
            || key.Equals("market", StringComparison.OrdinalIgnoreCase)
            || key.Equals("region", StringComparison.OrdinalIgnoreCase)
            || key.Equals("country", StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<string> BuildKnownBoundaryMarketTokens()
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in MarketAliases)
        {
            tokens.Add(entry.Key.ToLowerInvariant());
            foreach (var alias in entry.Value)
            {
                tokens.Add(alias.ToLowerInvariant());
            }
        }

        return tokens;
    }

    private static string? NormalizeLocaleToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var candidate = value.Trim().Replace('_', '-');
        var match = LocaleTokenRegex().Match(candidate);
        if (!match.Success)
        {
            return null;
        }

        return $"{match.Groups["language"].Value.ToLowerInvariant()}-{match.Groups["region"].Value.ToLowerInvariant()}";
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

    [GeneratedRegex("^(?<language>[a-z]{2})[-_](?<region>[a-z]{2})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LocaleTokenRegex();
}