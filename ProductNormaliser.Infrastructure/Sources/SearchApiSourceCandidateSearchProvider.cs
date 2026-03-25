using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using ProductNormaliser.Application.Sources;

namespace ProductNormaliser.Infrastructure.Sources;

public sealed class SearchApiSourceCandidateSearchProvider(HttpClient httpClient, IOptions<SourceCandidateDiscoveryOptions> options) : ISourceCandidateSearchProvider
{
    private static readonly Uri DefaultBaseAddress = new("https://api.search.brave.com", UriKind.Absolute);
    private readonly SourceCandidateDiscoveryOptions options = options.Value;

    public async Task<IReadOnlyList<SourceCandidateSearchResult>> SearchAsync(DiscoverSourceCandidatesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.SearchTimeoutSeconds)));

        var candidatesByHost = new Dictionary<string, SourceCandidateSearchResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var query in BuildQueries(request).Take(Math.Max(1, options.MaxSearchQueries)))
        {
            foreach (var candidate in await SearchQueryAsync(query, request, timeoutCts.Token))
            {
                var candidateKey = BuildCandidateGroupingKey(candidate);
                if (candidatesByHost.TryGetValue(candidateKey, out var existing))
                {
                    candidatesByHost[candidateKey] = new SourceCandidateSearchResult
                    {
                        CandidateKey = existing.CandidateKey,
                        DisplayName = existing.DisplayName.Length >= candidate.DisplayName.Length ? existing.DisplayName : candidate.DisplayName,
                        BaseUrl = existing.BaseUrl,
                        Host = existing.Host,
                        CandidateType = string.Equals(existing.CandidateType, "manufacturer", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(candidate.CandidateType, "manufacturer", StringComparison.OrdinalIgnoreCase)
                            ? "manufacturer"
                            : "retailer",
                        AllowedMarkets = existing.AllowedMarkets
                            .Concat(candidate.AllowedMarkets)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                            .ToArray(),
                        PreferredLocale = string.Equals(existing.PreferredLocale, request.Locale, StringComparison.OrdinalIgnoreCase)
                            ? existing.PreferredLocale
                            : candidate.PreferredLocale,
                        MarketEvidence = MergeEvidence(existing.MarketEvidence, candidate.MarketEvidence),
                        LocaleEvidence = MergeEvidence(existing.LocaleEvidence, candidate.LocaleEvidence),
                        MatchedCategoryKeys = existing.MatchedCategoryKeys
                            .Concat(candidate.MatchedCategoryKeys)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                            .ToArray(),
                        MatchedBrandHints = existing.MatchedBrandHints
                            .Concat(candidate.MatchedBrandHints)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                            .ToArray(),
                        SearchReasons = existing.SearchReasons
                            .Concat(candidate.SearchReasons)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                            .ToArray()
                    };
                }
                else
                {
                    candidatesByHost[candidateKey] = candidate;
                }
            }
        }

        return candidatesByHost.Values
            .OrderBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<string> BuildQueries(DiscoverSourceCandidatesRequest request)
    {
        var queries = new List<string>();
        var regionTerms = string.Join(' ', new[] { request.Market, request.Locale }.Where(value => !string.IsNullOrWhiteSpace(value)));

        foreach (var categoryKey in request.CategoryKeys)
        {
            foreach (var brandHint in request.BrandHints)
            {
                queries.Add($"{brandHint} {categoryKey} official site {regionTerms}".Trim());
            }

            queries.Add($"{categoryKey} retailer {regionTerms}".Trim());
            queries.Add($"buy {categoryKey} online {regionTerms}".Trim());
        }

        if (request.BrandHints.Count > 0)
        {
            queries.Add($"{string.Join(' ', request.BrandHints)} manufacturer official site {regionTerms}".Trim());
        }

        return queries
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<IReadOnlyList<SourceCandidateSearchResult>> SearchQueryAsync(string query, DiscoverSourceCandidatesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, BuildRequestUri(query));
            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests || !response.IsSuccessStatusCode)
            {
                return [];
            }

            var payload = await response.Content.ReadFromJsonAsync<SearchApiResponse>(cancellationToken: cancellationToken);
            if (payload?.Web?.Results is null || payload.Web.Results.Count == 0)
            {
                return [];
            }

            return payload.Web.Results
                .Select(result => TryMapCandidate(result, request, query))
                .OfType<SourceCandidateSearchResult>()
                .ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return [];
        }
    }

    private SourceCandidateSearchResult? TryMapCandidate(SearchApiWebResult result, DiscoverSourceCandidatesRequest request, string query)
    {
        if (string.IsNullOrWhiteSpace(result.Url)
            || !Uri.TryCreate(result.Url, UriKind.Absolute, out var uri)
            || !IsSupportedCandidateUri(uri))
        {
            return null;
        }

        var host = NormalizeHost(uri.Host);
        var title = result.Title?.Trim() ?? host;
        var description = result.Description?.Trim() ?? string.Empty;
        var combinedText = string.Join(' ', new[] { title, description, query, uri.AbsoluteUri });

        var matchedCategoryKeys = request.CategoryKeys
            .Where(categoryKey => combinedText.Contains(categoryKey, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var matchedBrandHints = request.BrandHints
            .Where(brandHint => combinedText.Contains(brandHint, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var candidateType = matchedBrandHints.Length > 0
            && (combinedText.Contains("official", StringComparison.OrdinalIgnoreCase)
                || combinedText.Contains("manufacturer", StringComparison.OrdinalIgnoreCase)
                || combinedText.Contains("brand", StringComparison.OrdinalIgnoreCase))
            ? "manufacturer"
            : "retailer";

        var allowedMarkets = InferAllowedMarkets(uri, request, out var marketEvidence);
        var preferredLocale = InferPreferredLocale(uri, request, out var localeEvidence);

        return new SourceCandidateSearchResult
        {
            CandidateKey = host.Replace('.', '_'),
            DisplayName = title,
            BaseUrl = uri.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/",
            Host = host,
            CandidateType = candidateType,
            AllowedMarkets = allowedMarkets,
            PreferredLocale = preferredLocale,
            MarketEvidence = marketEvidence,
            LocaleEvidence = localeEvidence,
            MatchedCategoryKeys = matchedCategoryKeys.Length > 0 ? matchedCategoryKeys : request.CategoryKeys.ToArray(),
            MatchedBrandHints = matchedBrandHints,
            SearchReasons = [$"Search query '{query}' matched '{title}'."]
        };
    }

    private static string BuildCandidateGroupingKey(SourceCandidateSearchResult candidate)
    {
        var marketKey = candidate.AllowedMarkets.Count == 0
            ? "global"
            : string.Join('+', candidate.AllowedMarkets.OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
        return $"{candidate.Host}::{marketKey}";
    }

    private static IReadOnlyList<string> InferAllowedMarkets(Uri uri, DiscoverSourceCandidatesRequest request, out string evidence)
    {
        var markets = new List<string>();
        var explicitSignals = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Market))
        {
            markets.Add(request.Market.Trim().ToUpperInvariant());
        }

        var host = uri.Host.ToLowerInvariant();
        if (host.EndsWith(".co.uk", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".uk", StringComparison.OrdinalIgnoreCase))
        {
            markets.Add("UK");
            explicitSignals.Add("UK");
        }

        if (uri.AbsolutePath.Contains("/uk", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.Contains("/en-gb", StringComparison.OrdinalIgnoreCase))
        {
            markets.Add("UK");
            explicitSignals.Add("UK");
        }

        var normalized = markets
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        evidence = normalized.Length switch
        {
            0 => "missing",
            _ when explicitSignals.Count > 0 && normalized.Length == 1 => "explicit",
            _ when explicitSignals.Count > 0 && normalized.Length > 1 => "ambiguous",
            _ => "request_hint"
        };

        return normalized;
    }

    private static string? InferPreferredLocale(Uri uri, DiscoverSourceCandidatesRequest request, out string evidence)
    {
        if (uri.AbsolutePath.Contains("/en-gb", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.Contains("/uk", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".co.uk", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".uk", StringComparison.OrdinalIgnoreCase))
        {
            evidence = "explicit";
            return "en-GB";
        }

        if (!string.IsNullOrWhiteSpace(request.Locale))
        {
            evidence = "request_hint";
            return request.Locale.Trim();
        }

        evidence = "missing";
        return null;
    }

    private static string MergeEvidence(string existing, string candidate)
    {
        return GetEvidenceStrength(existing) >= GetEvidenceStrength(candidate) ? existing : candidate;
    }

    private static int GetEvidenceStrength(string? evidence)
    {
        return evidence?.Trim().ToLowerInvariant() switch
        {
            "explicit" => 3,
            "request_hint" => 2,
            "ambiguous" => 1,
            _ => 0
        };
    }

    private static bool IsSupportedCandidateUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri)
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host)
            || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || IPAddress.TryParse(uri.Host, out _))
        {
            return false;
        }

        return true;
    }

    private static string NormalizeHost(string host)
    {
        var normalized = host.Trim().TrimEnd('.');
        return normalized.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? normalized[4..]
            : normalized;
    }

    private Uri BuildRequestUri(string query)
    {
        var configuredBaseAddress = Uri.TryCreate(options.SearchApiBaseUrl, UriKind.Absolute, out var parsed)
            ? parsed
            : DefaultBaseAddress;
        var baseAddress = httpClient.BaseAddress ?? configuredBaseAddress;
        var uri = new Uri(baseAddress, $"/res/v1/web/search?q={Uri.EscapeDataString(query)}&count=10");
        return uri;
    }

    private sealed class SearchApiResponse
    {
        public SearchApiWebResponse? Web { get; init; }
    }

    private sealed class SearchApiWebResponse
    {
        public IReadOnlyList<SearchApiWebResult> Results { get; init; } = [];
    }

    private sealed class SearchApiWebResult
    {
        public string? Title { get; init; }

        public string? Url { get; init; }

        public string? Description { get; init; }
    }
}