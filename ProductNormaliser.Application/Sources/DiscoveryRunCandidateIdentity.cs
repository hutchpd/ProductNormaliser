using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

public static class DiscoveryRunCandidateIdentity
{
    public static string NormalizeHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return string.Empty;
        }

        var normalized = host.Trim().TrimEnd('.');
        return normalized.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? normalized[4..].ToLowerInvariant()
            : normalized.ToLowerInvariant();
    }

    public static string NormalizeBaseUrl(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return baseUrl.Trim().TrimEnd('/').ToLowerInvariant();
        }

        var authority = uri.IsDefaultPort
            ? $"{uri.Scheme.ToLowerInvariant()}://{NormalizeHost(uri.Host)}"
            : $"{uri.Scheme.ToLowerInvariant()}://{NormalizeHost(uri.Host)}:{uri.Port}";
        var path = string.IsNullOrWhiteSpace(uri.AbsolutePath) ? "/" : uri.AbsolutePath.TrimEnd('/');
        return $"{authority}{path}";
    }

    public static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    public static IReadOnlyList<string> NormalizeMarkets(IReadOnlyCollection<string> markets)
    {
        return markets
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool ShareAnyMarket(IReadOnlyCollection<string> left, IReadOnlyCollection<string> right)
    {
        var leftSet = NormalizeMarkets(left).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rightSet = NormalizeMarkets(right).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (leftSet.Count == 0 || rightSet.Count == 0)
        {
            return false;
        }

        return leftSet.Overlaps(rightSet);
    }

    public static string GetNormalizedHost(DiscoveryRunCandidate candidate) => NormalizeHost(candidate.Host);

    public static string GetNormalizedBaseUrl(DiscoveryRunCandidate candidate) => NormalizeBaseUrl(candidate.BaseUrl);

    public static string GetNormalizedDisplayName(DiscoveryRunCandidate candidate) => NormalizeName(candidate.DisplayName);

    public static string GetNormalizedHost(SourceCandidateSearchResult candidate) => NormalizeHost(candidate.Host);

    public static string GetNormalizedBaseUrl(SourceCandidateSearchResult candidate) => NormalizeBaseUrl(candidate.BaseUrl);

    public static string GetNormalizedDisplayName(SourceCandidateSearchResult candidate) => NormalizeName(candidate.DisplayName);
}