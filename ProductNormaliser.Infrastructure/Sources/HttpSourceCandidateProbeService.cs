using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductNormaliser.Application.AI;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.AI;
using ProductNormaliser.Infrastructure.Crawling;

namespace ProductNormaliser.Infrastructure.Sources;

public sealed partial class HttpSourceCandidateProbeService(
    IHttpFetcher httpFetcher,
    IStructuredDataExtractor structuredDataExtractor,
    IPageClassificationService pageClassifier,
    IOptions<SourceCandidateDiscoveryOptions> options,
    IOptions<LlmOptions> llmOptions,
    ILogger<HttpSourceCandidateProbeService> logger) : ISourceCandidateProbeService
{
    private readonly SourceCandidateDiscoveryOptions options = options.Value;
    private readonly LlmOptions llmOptions = llmOptions.Value;

    public async Task<SourceCandidateProbeResult> ProbeAsync(SourceCandidateSearchResult candidate, IReadOnlyCollection<string> categoryKeys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(categoryKeys);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.ProbeTimeoutSeconds)));

        var homePageTask = TryFetchTextAsync(candidate.BaseUrl, timeoutCts.Token);
        var robotsTask = TryFetchTextAsync(new Uri(new Uri(candidate.BaseUrl, UriKind.Absolute), "/robots.txt").ToString(), timeoutCts.Token);

        await Task.WhenAll(homePageTask, robotsTask);

        var homePageHtml = await homePageTask;
        var robotsText = await robotsTask;
        var sitemapUrls = ExtractSitemapUrls(candidate.BaseUrl, robotsText, homePageHtml);
        var categoryPageHints = ExtractCategoryPageHints(homePageHtml, categoryKeys);
        var representativeCategoryPageUrl = categoryPageHints.FirstOrDefault();
        var representativeCategoryPageHtml = await TryFetchRepresentativeAsync(candidate.BaseUrl, representativeCategoryPageUrl, timeoutCts.Token);
        var representativeProductPageUrl = ExtractRepresentativeProductPageUrl(candidate.BaseUrl, homePageHtml, representativeCategoryPageHtml);
        var representativeProductPageHtml = await TryFetchRepresentativeAsync(candidate.BaseUrl, representativeProductPageUrl, timeoutCts.Token);
        var likelyListingUrlPatterns = InferListingUrlPatterns(categoryPageHints);
        var likelyProductUrlPatterns = InferProductUrlPatterns(string.Join('\n', new[] { homePageHtml, representativeCategoryPageHtml }.Where(value => !string.IsNullOrWhiteSpace(value))));
        var structuredProductEvidenceDetected = HasStructuredProductEvidence(representativeProductPageHtml, representativeProductPageUrl);
        var technicalAttributeEvidenceDetected = HasTechnicalAttributeEvidence(representativeProductPageHtml);
        var heuristicProductEvidenceDetected = structuredProductEvidenceDetected || technicalAttributeEvidenceDetected;
        var requestedCategory = categoryKeys
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault() ?? "product";
        var llmResult = await TryClassifyRepresentativeProductPageAsync(representativeProductPageHtml, requestedCategory, timeoutCts.Token);
        var llmNeutral = llmResult is not null && IsNeutralLlmResult(llmResult);
        var llmAcceptedRepresentativeProductPage = llmResult is not null
            && !llmNeutral
            && llmResult.IsProductPage
            && llmResult.HasSpecifications
            && llmResult.Confidence >= llmOptions.ConfidenceThreshold;
        var llmRejectedRepresentativeProductPage = llmResult is not null
            && !llmNeutral
            && !llmResult.IsProductPage;
        var llmDisagreedWithHeuristics = llmResult is not null && llmAcceptedRepresentativeProductPage != heuristicProductEvidenceDetected;
        var catalogLikelihoodScore = ScoreCatalogLikelihood(homePageHtml, representativeCategoryPageHtml, representativeProductPageUrl);
        var categoryRelevanceScore = ScoreCategoryRelevance(categoryKeys, homePageHtml, representativeCategoryPageHtml, categoryPageHints);
        var crawlabilityScore = ScoreCrawlability(homePageHtml, robotsText, sitemapUrls.Count > 0, representativeCategoryPageHtml, representativeProductPageHtml);
        var heuristicExtractabilityScore = ScoreExtractability(structuredProductEvidenceDetected, technicalAttributeEvidenceDetected, representativeProductPageHtml);
        var extractabilityScore = AdjustExtractabilityScoreForLlm(
            heuristicExtractabilityScore,
            llmAcceptedRepresentativeProductPage,
            llmRejectedRepresentativeProductPage,
            heuristicProductEvidenceDetected);

        if (llmOptions.EvaluationMode)
        {
            logger.LogInformation(
                "LLM evaluation: heuristicProduct={HeuristicProduct}, llmProduct={LlmProduct}, finalAccepted={FinalAccepted}, llmReason={LlmReason}, llmConfidence={LlmConfidence}",
                heuristicProductEvidenceDetected,
                llmResult?.IsProductPage,
                llmAcceptedRepresentativeProductPage,
                llmResult?.Reason,
                llmResult?.Confidence);
        }

        return new SourceCandidateProbeResult
        {
            HomePageReachable = homePageHtml is not null,
            RobotsTxtReachable = robotsText is not null,
            SitemapDetected = sitemapUrls.Count > 0,
            SitemapUrls = sitemapUrls,
            CrawlabilityScore = crawlabilityScore,
            CategoryRelevanceScore = categoryRelevanceScore,
            HeuristicExtractabilityScore = heuristicExtractabilityScore,
            ExtractabilityScore = extractabilityScore,
            CatalogLikelihoodScore = catalogLikelihoodScore,
            RepresentativeCategoryPageUrl = representativeCategoryPageUrl,
            RepresentativeCategoryPageReachable = representativeCategoryPageHtml is not null,
            RepresentativeProductPageUrl = representativeProductPageUrl,
            RepresentativeProductPageReachable = representativeProductPageHtml is not null,
            StructuredProductEvidenceDetected = structuredProductEvidenceDetected,
            TechnicalAttributeEvidenceDetected = technicalAttributeEvidenceDetected,
            LlmAcceptedRepresentativeProductPage = llmAcceptedRepresentativeProductPage,
            LlmRejectedRepresentativeProductPage = llmRejectedRepresentativeProductPage,
            LlmDisagreedWithHeuristics = llmDisagreedWithHeuristics,
            LlmDetectedSpecifications = llmResult?.HasSpecifications == true,
            LlmDetectedCategory = llmResult?.DetectedCategory,
            LlmConfidenceScore = llmResult is null ? null : decimal.Round((decimal)llmResult.Confidence * 100m, 2, MidpointRounding.AwayFromZero),
            LlmReason = llmResult?.Reason,
            NonCatalogContentHeavy = catalogLikelihoodScore <= 40m,
            CategoryPageHints = categoryPageHints,
            LikelyListingUrlPatterns = likelyListingUrlPatterns,
            LikelyProductUrlPatterns = likelyProductUrlPatterns
        };
    }

    private async Task<string?> TryFetchRepresentativeAsync(string baseUrl, string? candidateUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(candidateUrl))
        {
            return null;
        }

        if (!Uri.TryCreate(new Uri(baseUrl, UriKind.Absolute), candidateUrl, out var absoluteUri))
        {
            return null;
        }

        return await TryFetchTextAsync(absoluteUri.ToString(), cancellationToken);
    }

    private async Task<string?> TryFetchTextAsync(string absoluteUrl, CancellationToken cancellationToken)
    {
        var result = await httpFetcher.FetchAsync(new CrawlTarget
        {
            Url = absoluteUrl,
            CategoryKey = string.Empty
        }, cancellationToken);

        return result.IsSuccess ? result.Html ?? string.Empty : null;
    }

    private static IReadOnlyList<string> ExtractSitemapUrls(string baseUrl, string? robotsTxt, string? homePageHtml)
    {
        var baseUri = new Uri(baseUrl, UriKind.Absolute);
        var sitemapUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(robotsTxt))
        {
            foreach (Match match in SitemapDirectiveRegex().Matches(robotsTxt))
            {
                var candidate = match.Groups["url"].Value.Trim();
                if (Uri.TryCreate(baseUri, candidate, out var sitemapUri))
                {
                    sitemapUrls.Add(sitemapUri.ToString());
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(homePageHtml))
        {
            foreach (var href in ExtractHrefs(homePageHtml))
            {
                if (!href.Contains("sitemap", StringComparison.OrdinalIgnoreCase)
                    || !href.Contains(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Uri.TryCreate(baseUri, href, out var sitemapUri))
                {
                    sitemapUrls.Add(sitemapUri.ToString());
                }
            }
        }

        return sitemapUrls
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ExtractCategoryPageHints(string? homePageHtml, IReadOnlyCollection<string> categoryKeys)
    {
        if (string.IsNullOrWhiteSpace(homePageHtml) || categoryKeys.Count == 0)
        {
            return [];
        }

        var hints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var href in ExtractHrefs(homePageHtml))
        {
            var normalizedHref = href.Trim();
            if (string.IsNullOrWhiteSpace(normalizedHref))
            {
                continue;
            }

            if (normalizedHref.Contains("/category/", StringComparison.OrdinalIgnoreCase)
                || normalizedHref.Contains("/department/", StringComparison.OrdinalIgnoreCase)
                || normalizedHref.Contains("/shop/", StringComparison.OrdinalIgnoreCase)
                || normalizedHref.Contains("/browse/", StringComparison.OrdinalIgnoreCase))
            {
                hints.Add(normalizedHref);
                continue;
            }

            foreach (var categoryKey in categoryKeys)
            {
                if (normalizedHref.Contains(categoryKey, StringComparison.OrdinalIgnoreCase)
                    || normalizedHref.Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase).Contains(categoryKey.Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase), StringComparison.OrdinalIgnoreCase)
                    || normalizedHref.Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase).Contains(categoryKey.Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase), StringComparison.OrdinalIgnoreCase))
                {
                    hints.Add(normalizedHref);
                    break;
                }
            }
        }

        return hints
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();
    }

    private static IReadOnlyList<string> InferListingUrlPatterns(IEnumerable<string> categoryPageHints)
    {
        var patterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var hint in categoryPageHints)
        {
            if (!Uri.TryCreate(hint, UriKind.Absolute, out var uri) && !Uri.TryCreate($"https://candidate.local{(hint.StartsWith('/') ? string.Empty : "/")}{hint}", UriKind.Absolute, out uri))
            {
                continue;
            }

            var path = uri.AbsolutePath;
            if (path.Contains("/category/", StringComparison.OrdinalIgnoreCase))
            {
                patterns.Add("/category/");
                continue;
            }

            if (path.Contains("/department/", StringComparison.OrdinalIgnoreCase))
            {
                patterns.Add("/department/");
                continue;
            }

            var firstSegment = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstSegment))
            {
                patterns.Add($"/{firstSegment}/");
            }
        }

        return patterns.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string? ExtractRepresentativeProductPageUrl(string baseUrl, params string?[] htmlFragments)
    {
        foreach (var html in htmlFragments)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                continue;
            }

            foreach (var href in ExtractHrefs(html))
            {
                if (!LooksLikeProductLink(href))
                {
                    continue;
                }

                if (Uri.TryCreate(new Uri(baseUrl, UriKind.Absolute), href, out var productUri))
                {
                    return productUri.ToString();
                }
            }
        }

        return null;
    }

    private static IReadOnlyList<string> InferProductUrlPatterns(string? homePageHtml)
    {
        if (string.IsNullOrWhiteSpace(homePageHtml))
        {
            return [];
        }

        var patterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var href in ExtractHrefs(homePageHtml))
        {
            if (href.Contains("/product/", StringComparison.OrdinalIgnoreCase))
            {
                patterns.Add("/product/");
            }

            if (href.Contains("/products/", StringComparison.OrdinalIgnoreCase))
            {
                patterns.Add("/products/");
            }

            if (href.Contains("/p/", StringComparison.OrdinalIgnoreCase))
            {
                patterns.Add("/p/");
            }

            if (href.Contains("/dp/", StringComparison.OrdinalIgnoreCase))
            {
                patterns.Add("/dp/");
            }

            if (href.Contains("/item/", StringComparison.OrdinalIgnoreCase))
            {
                patterns.Add("/item/");
            }
        }

        return patterns.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static decimal ScoreCategoryRelevance(IReadOnlyCollection<string> categoryKeys, string? homePageHtml, string? representativeCategoryPageHtml, IEnumerable<string> categoryPageHints)
    {
        if (categoryKeys.Count == 0)
        {
            return 0m;
        }

        var score = 0m;
        var html = string.Join(' ', new[] { homePageHtml, representativeCategoryPageHtml }.Where(value => !string.IsNullOrWhiteSpace(value)));
        foreach (var categoryKey in categoryKeys.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (html.Contains(categoryKey, StringComparison.OrdinalIgnoreCase))
            {
                score += 15m;
            }
        }

        var hintCount = categoryPageHints.Count();
        score += Math.Min(55m, hintCount * 12m);

        return Math.Min(100m, score);
    }

    private decimal ScoreExtractability(bool structuredProductEvidenceDetected, bool technicalAttributeEvidenceDetected, string? representativeProductPageHtml)
    {
        var score = 0m;
        if (!string.IsNullOrWhiteSpace(representativeProductPageHtml))
        {
            score += 10m;
        }

        if (structuredProductEvidenceDetected)
        {
            score += 60m;
        }

        if (technicalAttributeEvidenceDetected)
        {
            score += 30m;
        }

        return Math.Min(100m, score);
    }

    private static decimal AdjustExtractabilityScoreForLlm(decimal extractabilityScore, bool llmAcceptedRepresentativeProductPage, bool llmRejectedRepresentativeProductPage, bool heuristicProductEvidenceDetected)
    {
        if (llmRejectedRepresentativeProductPage)
        {
            return Math.Max(0m, extractabilityScore - 35m);
        }

        if (llmAcceptedRepresentativeProductPage && heuristicProductEvidenceDetected)
        {
            return Math.Min(100m, extractabilityScore + 10m);
        }

        return extractabilityScore;
    }

    private async Task<PageClassificationResult?> TryClassifyRepresentativeProductPageAsync(string? representativeProductPageHtml, string category, CancellationToken cancellationToken)
    {
        if (!llmOptions.Enabled || string.IsNullOrWhiteSpace(representativeProductPageHtml))
        {
            return null;
        }

        try
        {
            return await pageClassifier.ClassifyAsync(representativeProductPageHtml, category, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Representative product-page classification failed for category {Category} during candidate probing.", category);
            return new PageClassificationResult
            {
                IsProductPage = false,
                HasSpecifications = false,
                Confidence = 0d,
                Reason = "LLM unavailable"
            };
        }
    }

    private static bool IsNeutralLlmResult(PageClassificationResult result)
    {
        return string.Equals(result.Reason, "LLM unavailable", StringComparison.OrdinalIgnoreCase)
            || string.Equals(result.Reason, "LLM timeout", StringComparison.OrdinalIgnoreCase)
            || string.Equals(result.Reason, "LLM low confidence", StringComparison.OrdinalIgnoreCase)
            || string.Equals(result.Reason, "LLM disabled", StringComparison.OrdinalIgnoreCase);
    }

    private static decimal ScoreCrawlability(string? homePageHtml, string? robotsText, bool sitemapDetected, string? representativeCategoryPageHtml, string? representativeProductPageHtml)
    {
        var score = 0m;
        if (homePageHtml is not null)
        {
            score += 30m;
        }

        if (robotsText is not null)
        {
            score += 20m;
        }

        if (sitemapDetected)
        {
            score += 20m;
        }

        if (representativeCategoryPageHtml is not null)
        {
            score += 15m;
        }

        if (representativeProductPageHtml is not null)
        {
            score += 15m;
        }

        return Math.Min(100m, score);
    }

    private static decimal ScoreCatalogLikelihood(string? homePageHtml, string? representativeCategoryPageHtml, string? representativeProductPageUrl)
    {
        var catalogSignals = 0;
        var nonCatalogSignals = 0;

        foreach (var href in ExtractHrefs(string.Join('\n', new[] { homePageHtml, representativeCategoryPageHtml }.Where(value => !string.IsNullOrWhiteSpace(value)))))
        {
            if (LooksLikeCatalogLink(href))
            {
                catalogSignals++;
            }

            if (LooksLikeNonCatalogLink(href))
            {
                nonCatalogSignals++;
            }
        }

        if (!string.IsNullOrWhiteSpace(representativeProductPageUrl))
        {
            catalogSignals += 3;
        }

        var raw = 50m + (catalogSignals * 6m) - (nonCatalogSignals * 7m);
        return Math.Clamp(raw, 0m, 100m);
    }

    private bool HasStructuredProductEvidence(string? html, string? url)
    {
        if (string.IsNullOrWhiteSpace(html) || string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (html.Contains("application/ld+json", StringComparison.OrdinalIgnoreCase)
            && html.Contains("Product", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return structuredDataExtractor.ExtractProducts(html, url).Count > 0
            || html.Contains("\"@type\":\"Product\"", StringComparison.OrdinalIgnoreCase)
            || html.Contains("\"@type\": \"Product\"", StringComparison.OrdinalIgnoreCase)
            || html.Contains("'@type':'Product'", StringComparison.OrdinalIgnoreCase)
            || html.Contains("schema.org/Product", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasTechnicalAttributeEvidence(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return false;
        }

        if ((html.Contains("specifications", StringComparison.OrdinalIgnoreCase)
                || html.Contains("technical details", StringComparison.OrdinalIgnoreCase)
                || html.Contains("product details", StringComparison.OrdinalIgnoreCase))
            && TableSignalRegex().IsMatch(html))
        {
            return true;
        }

        var signalCount = 0;
        if (html.Contains("specifications", StringComparison.OrdinalIgnoreCase)
            || html.Contains("technical details", StringComparison.OrdinalIgnoreCase)
            || html.Contains("key features", StringComparison.OrdinalIgnoreCase)
            || html.Contains("product details", StringComparison.OrdinalIgnoreCase))
        {
            signalCount += 2;
        }

        if (TableSignalRegex().IsMatch(html))
        {
            signalCount += 2;
        }

        if (TechAttributeRegex().Matches(html).Count >= 2)
        {
            signalCount += 2;
        }

        return signalCount >= 2;
    }

    private static bool LooksLikeCatalogLink(string href)
    {
        return href.Contains("/product/", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/products/", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/p/", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/category/", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/shop/", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/browse/", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/department/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeProductLink(string href)
    {
        if (href.Contains("/category/", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/department/", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/shop/", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/browse/", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/support", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/help", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/blog", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/news", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return href.Contains("/product/", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/products/", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/p/", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/dp/", StringComparison.OrdinalIgnoreCase)
            || ProductSlugRegex().IsMatch(href);
    }

    private static bool LooksLikeNonCatalogLink(string href)
    {
        return href.Contains("/support", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/help", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/blog", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/news", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/contact", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/about", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/search", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ExtractHrefs(string html)
    {
        return HrefRegex().Matches(html)
            .Select(match => match.Groups["href"].Value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    [GeneratedRegex("^\\s*Sitemap:\\s*(?<url>\\S+)\\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex SitemapDirectiveRegex();

    [GeneratedRegex("href\\s*=\\s*[\"'](?<href>[^\"'#>]+)[\"']", RegexOptions.IgnoreCase)]
    private static partial Regex HrefRegex();

    [GeneratedRegex("<(table|dl|th|td)\\b", RegexOptions.IgnoreCase)]
    private static partial Regex TableSignalRegex();

    [GeneratedRegex("(screen size|resolution|hdmi|refresh rate|panel type|processor|memory|storage|weight|dimensions|bluetooth|wifi)", RegexOptions.IgnoreCase)]
    private static partial Regex TechAttributeRegex();

    [GeneratedRegex("/(?:[a-z0-9-]+/){1,4}[a-z0-9-]{4,}$", RegexOptions.IgnoreCase)]
    private static partial Regex ProductSlugRegex();
}