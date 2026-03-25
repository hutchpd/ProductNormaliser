using ProductNormaliser.Application.Categories;
using ProductNormaliser.Application.Governance;

namespace ProductNormaliser.Application.Sources;

public sealed class SourceCandidateDiscoveryService(
    ICrawlSourceStore crawlSourceStore,
    ICategoryMetadataService categoryMetadataService,
    ICrawlGovernanceService crawlGovernanceService,
    ISourceCandidateSearchProvider sourceCandidateSearchProvider,
    ISourceCandidateProbeService sourceCandidateProbeService) : ISourceCandidateDiscoveryService
{
    public async Task<SourceCandidateDiscoveryResult> DiscoverAsync(DiscoverSourceCandidatesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var categoryKeys = NormalizeValues(request.CategoryKeys);
        if (categoryKeys.Count == 0)
        {
            throw new ArgumentException("Choose at least one category before discovering source candidates.", nameof(request));
        }

        var knownCategoryKeys = (await categoryMetadataService.ListAsync(enabledOnly: false, cancellationToken))
            .Select(category => category.CategoryKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknownCategoryKeys = categoryKeys
            .Where(categoryKey => !knownCategoryKeys.Contains(categoryKey))
            .ToArray();
        if (unknownCategoryKeys.Length > 0)
        {
            throw new ArgumentException($"Unknown category keys: {string.Join(", ", unknownCategoryKeys)}.", nameof(request));
        }

        var normalizedRequest = new DiscoverSourceCandidatesRequest
        {
            CategoryKeys = categoryKeys,
            Locale = NormalizeOptionalText(request.Locale),
            Market = NormalizeOptionalText(request.Market),
            BrandHints = NormalizeValues(request.BrandHints),
            MaxCandidates = NormalizeMaxCandidates(request.MaxCandidates)
        };

        var registeredSources = await crawlSourceStore.ListAsync(cancellationToken);
        var searchResults = CollapseEquivalentCandidates(await sourceCandidateSearchProvider.SearchAsync(normalizedRequest, cancellationToken));

        var candidates = new List<SourceCandidateResult>(searchResults.Count);
        foreach (var searchResult in searchResults)
        {
            var duplicateSources = registeredSources
                .Where(source => IsPotentialDuplicate(source, searchResult))
                .ToArray();
            var probe = await sourceCandidateProbeService.ProbeAsync(searchResult, categoryKeys, cancellationToken);

            var governanceWarning = default(string);
            var allowedByGovernance = true;
            try
            {
                crawlGovernanceService.ValidateSourceBaseUrl(searchResult.BaseUrl, nameof(searchResult.BaseUrl));
            }
            catch (ArgumentException exception)
            {
                allowedByGovernance = false;
                governanceWarning = exception.Message;
            }

            var reasons = BuildReasons(searchResult, probe, duplicateSources, governanceWarning);
            candidates.Add(new SourceCandidateResult
            {
                CandidateKey = string.IsNullOrWhiteSpace(searchResult.CandidateKey) ? searchResult.Host : searchResult.CandidateKey,
                DisplayName = searchResult.DisplayName,
                BaseUrl = searchResult.BaseUrl,
                Host = searchResult.Host,
                CandidateType = searchResult.CandidateType,
                ConfidenceScore = CalculateConfidenceScore(probe, duplicateSources, allowedByGovernance),
                MatchedCategoryKeys = NormalizeValues(searchResult.MatchedCategoryKeys),
                MatchedBrandHints = NormalizeValues(searchResult.MatchedBrandHints),
                AlreadyRegistered = duplicateSources.Any(source => string.Equals(source.Host, searchResult.Host, StringComparison.OrdinalIgnoreCase)),
                DuplicateSourceIds = duplicateSources.Select(source => source.Id).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
                DuplicateSourceDisplayNames = duplicateSources.Select(source => source.DisplayName).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
                AllowedByGovernance = allowedByGovernance,
                GovernanceWarning = governanceWarning,
                Probe = probe,
                Reasons = reasons
            });
        }

        return new SourceCandidateDiscoveryResult
        {
            RequestedCategoryKeys = categoryKeys,
            Locale = normalizedRequest.Locale,
            Market = normalizedRequest.Market,
            BrandHints = NormalizeValues(normalizedRequest.BrandHints),
            GeneratedUtc = DateTime.UtcNow,
            Candidates = candidates
                .OrderByDescending(candidate => candidate.ConfidenceScore)
                .ThenBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Take(normalizedRequest.MaxCandidates)
                .ToArray()
        };
    }

    private static IReadOnlyList<SourceCandidateSearchResult> CollapseEquivalentCandidates(IReadOnlyList<SourceCandidateSearchResult> candidates)
    {
        if (candidates.Count <= 1)
        {
            return candidates;
        }

        var groups = candidates
            .GroupBy(GetCandidateEquivalenceKey, StringComparer.OrdinalIgnoreCase);
        var collapsed = new List<SourceCandidateSearchResult>();
        foreach (var group in groups)
        {
            var ordered = group
                .OrderByDescending(GetSearchSignalScore)
                .ThenBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var preferred = ordered[0];

            collapsed.Add(new SourceCandidateSearchResult
            {
                CandidateKey = string.IsNullOrWhiteSpace(preferred.CandidateKey) ? preferred.Host : preferred.CandidateKey,
                DisplayName = preferred.DisplayName,
                BaseUrl = preferred.BaseUrl,
                Host = preferred.Host,
                CandidateType = ordered.Any(candidate => string.Equals(candidate.CandidateType, "manufacturer", StringComparison.OrdinalIgnoreCase))
                    ? "manufacturer"
                    : preferred.CandidateType,
                MatchedCategoryKeys = ordered.SelectMany(candidate => candidate.MatchedCategoryKeys)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                MatchedBrandHints = ordered.SelectMany(candidate => candidate.MatchedBrandHints)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                SearchReasons = ordered.SelectMany(candidate => candidate.SearchReasons)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            });
        }

        return collapsed;
    }

    private static IReadOnlyList<SourceCandidateReason> BuildReasons(
        SourceCandidateSearchResult searchResult,
        SourceCandidateProbeResult probe,
        IReadOnlyCollection<Core.Models.CrawlSource> duplicateSources,
        string? governanceWarning)
    {
        var reasons = new List<SourceCandidateReason>();

        reasons.AddRange(searchResult.SearchReasons.Select(reason => new SourceCandidateReason
        {
            Code = "search_match",
            Message = reason,
            Weight = 10m
        }));

        if (probe.RobotsTxtReachable)
        {
            reasons.Add(new SourceCandidateReason { Code = "robots", Message = "robots.txt was reachable during probing.", Weight = 15m });
        }

        if (probe.SitemapDetected)
        {
            reasons.Add(new SourceCandidateReason { Code = "sitemap", Message = "Sitemap hints were detected for this host.", Weight = 20m });
        }

        if (probe.CategoryRelevanceScore > 0m)
        {
            reasons.Add(new SourceCandidateReason
            {
                Code = "category_relevance",
                Message = $"Category relevance scored {probe.CategoryRelevanceScore:0.##}.",
                Weight = probe.CategoryRelevanceScore
            });
        }

        if (duplicateSources.Count > 0)
        {
            reasons.Add(new SourceCandidateReason
            {
                Code = "duplicate",
                Message = $"Potential duplicate of registered sources: {string.Join(", ", duplicateSources.Select(source => source.DisplayName).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))}.",
                Weight = -40m
            });
        }

        if (!string.IsNullOrWhiteSpace(governanceWarning))
        {
            reasons.Add(new SourceCandidateReason
            {
                Code = "governance",
                Message = governanceWarning,
                Weight = -100m
            });
        }

        return reasons
            .OrderByDescending(reason => reason.Weight)
            .ThenBy(reason => reason.Message, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static decimal CalculateConfidenceScore(SourceCandidateProbeResult probe, IReadOnlyCollection<Core.Models.CrawlSource> duplicateSources, bool allowedByGovernance)
    {
        var score = 15m;
        if (probe.HomePageReachable)
        {
            score += 10m;
        }

        if (probe.RobotsTxtReachable)
        {
            score += 15m;
        }

        if (probe.SitemapDetected)
        {
            score += 20m;
        }

        score += Math.Clamp(probe.CategoryRelevanceScore, 0m, 40m);

        if (duplicateSources.Count > 0)
        {
            score -= 35m;
        }

        if (!allowedByGovernance)
        {
            score = Math.Min(score, 10m);
        }

        return Math.Clamp(decimal.Round(score, 2, MidpointRounding.AwayFromZero), 0m, 100m);
    }

    private static bool IsPotentialDuplicate(Core.Models.CrawlSource source, SourceCandidateSearchResult candidate)
    {
        if (string.Equals(NormalizeHost(source.Host), NormalizeHost(candidate.Host), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(NormalizeBaseUrl(source.BaseUrl), NormalizeBaseUrl(candidate.BaseUrl), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(NormalizeName(source.DisplayName), NormalizeName(candidate.DisplayName), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeName(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static string GetCandidateEquivalenceKey(SourceCandidateSearchResult candidate)
    {
        var normalizedHost = NormalizeHost(candidate.Host);
        if (!string.IsNullOrWhiteSpace(normalizedHost))
        {
            return $"host:{normalizedHost}";
        }

        return $"base:{NormalizeBaseUrl(candidate.BaseUrl)}";
    }

    private static int GetSearchSignalScore(SourceCandidateSearchResult candidate)
    {
        var score = 0;
        score += candidate.MatchedCategoryKeys.Count * 10;
        score += candidate.MatchedBrandHints.Count * 8;
        score += candidate.SearchReasons.Count * 3;
        if (string.Equals(candidate.CandidateType, "manufacturer", StringComparison.OrdinalIgnoreCase))
        {
            score += 5;
        }

        return score;
    }

    private static string NormalizeBaseUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return value.Trim().TrimEnd('/').ToLowerInvariant();
        }

        var authority = uri.IsDefaultPort
            ? $"{uri.Scheme.ToLowerInvariant()}://{NormalizeHost(uri.Host)}"
            : $"{uri.Scheme.ToLowerInvariant()}://{NormalizeHost(uri.Host)}:{uri.Port}";
        var path = string.IsNullOrWhiteSpace(uri.AbsolutePath) ? "/" : uri.AbsolutePath.TrimEnd('/');
        return $"{authority}{path}";
    }

    private static string NormalizeHost(string value)
    {
        var normalized = value.Trim().TrimEnd('.');
        return normalized.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? normalized[4..]
            : normalized;
    }

    private static List<string> NormalizeValues(IEnumerable<string>? values)
    {
        return (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int NormalizeMaxCandidates(int value)
    {
        if (value <= 0)
        {
            return 10;
        }

        return Math.Min(25, value);
    }
}