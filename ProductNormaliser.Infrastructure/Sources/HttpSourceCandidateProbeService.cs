using System.Diagnostics;
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
    IOptions<SourceOnboardingAutomationOptions> onboardingAutomationOptions,
    IOptions<LlmOptions> llmOptions,
    ILogger<HttpSourceCandidateProbeService> logger) : ISourceCandidateProbeService
{
    private readonly SourceCandidateDiscoveryOptions options = options.Value;
    private readonly SourceOnboardingAutomationOptions onboardingAutomationOptions = onboardingAutomationOptions.Value;
    private readonly LlmOptions llmOptions = llmOptions.Value;

    public async Task<SourceCandidateProbeResult> ProbeAsync(
        SourceCandidateSearchResult candidate,
        IReadOnlyCollection<string> categoryKeys,
        string automationMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(categoryKeys);

        var maxRetries = Math.Max(0, options.ProbeRetryCount);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await ProbeOnceAsync(candidate, categoryKeys, automationMode, attempt + 1, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < maxRetries)
            {
                var retryDelay = GetRetryDelay(attempt, options.ProbeRetryBaseDelayMs);
                logger.LogInformation(
                    "Candidate probing timed out for {Host} on attempt {Attempt}. Retrying after {RetryDelayMs}ms.",
                    candidate.Host,
                    attempt + 1,
                    retryDelay.TotalMilliseconds);
                await Task.Delay(retryDelay, cancellationToken);
            }
        }
    }

    private async Task<SourceCandidateProbeResult> ProbeOnceAsync(
        SourceCandidateSearchResult candidate,
        IReadOnlyCollection<string> categoryKeys,
        string automationMode,
        int attemptNumber,
        CancellationToken cancellationToken)
    {
        var attemptStopwatch = Stopwatch.StartNew();

        var normalizedAutomationMode = SourceAutomationModes.Normalize(automationMode);
        var collectAutomationEvidence = normalizedAutomationMode is SourceAutomationModes.SuggestAccept or SourceAutomationModes.AutoAcceptAndSeed;

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
        var categorySampleUrls = collectAutomationEvidence
            ? SelectSampleUrls(candidate.BaseUrl, categoryPageHints, representativeCategoryPageUrl, onboardingAutomationOptions.AutomationCategorySampleBudget)
            : [];
        var categorySamples = categorySampleUrls.Count > 0
            ? await FetchSamplePagesAsync(categorySampleUrls, timeoutCts.Token)
            : [];
        var representativeCategoryPageHtml = FindFetchedHtml(categorySamples, ToAbsoluteUrl(candidate.BaseUrl, representativeCategoryPageUrl))
            ?? await TryFetchRepresentativeAsync(candidate.BaseUrl, representativeCategoryPageUrl, timeoutCts.Token);
        var representativeProductPageUrl = ExtractRepresentativeProductPageUrl(candidate.BaseUrl, homePageHtml, representativeCategoryPageHtml);
        var productSampleUrls = collectAutomationEvidence
            ? SelectSampleUrls(
                candidate.BaseUrl,
                ExtractProductPageUrls(candidate.BaseUrl, [homePageHtml, representativeCategoryPageHtml, .. categorySamples.Select(sample => sample.Html)]),
                representativeProductPageUrl,
                onboardingAutomationOptions.AutomationProductSampleBudget)
            : [];
        var productSamples = productSampleUrls.Count > 0
            ? await FetchSamplePagesAsync(productSampleUrls, timeoutCts.Token)
            : [];
        var representativeProductPageHtml = FindFetchedHtml(productSamples, representativeProductPageUrl)
            ?? await TryFetchRepresentativeAsync(candidate.BaseUrl, representativeProductPageUrl, timeoutCts.Token);
        var likelyListingUrlPatterns = InferListingUrlPatterns(categoryPageHints);
        var likelyProductUrlPatterns = InferProductUrlPatterns(string.Join(
            '\n',
            new[] { homePageHtml, representativeCategoryPageHtml }
                .Concat(categorySamples.Select(sample => sample.Html))
                .Where(value => !string.IsNullOrWhiteSpace(value))));
        var representativeEvidence = AnalyzeProductSample(representativeProductPageHtml, representativeProductPageUrl);
        var representativeRuntimeProductCount = representativeEvidence.RuntimeProductCount;
        var runtimeExtractionCompatible = representativeRuntimeProductCount > 0;
        var structuredProductEvidenceDetected = representativeEvidence.StructuredProductEvidenceDetected;
        var technicalAttributeEvidenceDetected = representativeEvidence.TechnicalAttributeEvidenceDetected;
        var heuristicProductEvidenceDetected = structuredProductEvidenceDetected || technicalAttributeEvidenceDetected;
        var requestedCategory = categoryKeys
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault() ?? "product";
        var timedClassification = await TryClassifyRepresentativeProductPageAsync(representativeProductPageHtml, requestedCategory, timeoutCts.Token);
        var llmResult = timedClassification.Result;
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
        var heuristicExtractabilityScore = ScoreExtractability(runtimeExtractionCompatible, structuredProductEvidenceDetected, technicalAttributeEvidenceDetected, representativeProductPageHtml);
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

        var automationProductEvidence = collectAutomationEvidence
            ? productSamples.Select(sample => AnalyzeProductSample(sample.Html, sample.Url)).ToArray()
            : [];

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
            RuntimeExtractionCompatible = runtimeExtractionCompatible,
            RepresentativeRuntimeProductCount = representativeRuntimeProductCount,
            AutomationCategorySampleCount = categorySamples.Count,
            AutomationReachableCategorySampleCount = categorySamples.Count(sample => sample.Html is not null),
            AutomationProductSampleCount = productSamples.Count,
            AutomationReachableProductSampleCount = automationProductEvidence.Count(sample => sample.PageReachable),
            AutomationRuntimeCompatibleProductSampleCount = automationProductEvidence.Count(sample => sample.RuntimeProductCount > 0),
            AutomationStructuredProductEvidenceSampleCount = automationProductEvidence.Count(sample => sample.StructuredProductEvidenceDetected),
            AutomationTechnicalAttributeEvidenceSampleCount = automationProductEvidence.Count(sample => sample.TechnicalAttributeEvidenceDetected),
            StructuredProductEvidenceDetected = structuredProductEvidenceDetected,
            TechnicalAttributeEvidenceDetected = technicalAttributeEvidenceDetected,
            LlmAcceptedRepresentativeProductPage = llmAcceptedRepresentativeProductPage,
            LlmRejectedRepresentativeProductPage = llmRejectedRepresentativeProductPage,
            LlmDisagreedWithHeuristics = llmDisagreedWithHeuristics,
            LlmDetectedSpecifications = llmResult?.HasSpecifications == true,
            LlmDetectedCategory = llmResult?.DetectedCategory,
            LlmConfidenceScore = llmResult is null ? null : decimal.Round((decimal)llmResult.Confidence * 100m, 2, MidpointRounding.AwayFromZero),
            LlmReason = llmResult?.Reason,
            ProbeAttemptCount = attemptNumber,
            ProbeElapsedMs = attemptStopwatch.ElapsedMilliseconds,
            LlmElapsedMs = timedClassification.ElapsedMs > 0 ? timedClassification.ElapsedMs : null,
            NonCatalogContentHeavy = catalogLikelihoodScore <= 40m,
            CategoryPageHints = categoryPageHints,
            LikelyListingUrlPatterns = likelyListingUrlPatterns,
            LikelyProductUrlPatterns = likelyProductUrlPatterns
        };
    }

    private async Task<string?> TryFetchRepresentativeAsync(string baseUrl, string? candidateUrl, CancellationToken cancellationToken)
    {
        var absoluteUrl = ToAbsoluteUrl(baseUrl, candidateUrl);
        if (absoluteUrl is null)
        {
            return null;
        }

        return await TryFetchTextAsync(absoluteUrl, cancellationToken);
    }

    private async Task<IReadOnlyList<FetchedPageSample>> FetchSamplePagesAsync(IReadOnlyList<string> absoluteUrls, CancellationToken cancellationToken)
    {
        if (absoluteUrls.Count == 0)
        {
            return [];
        }

        var htmlByUrl = await Task.WhenAll(absoluteUrls.Select(url => TryFetchTextAsync(url, cancellationToken)));
        return absoluteUrls
            .Zip(htmlByUrl, static (url, html) => new FetchedPageSample(url, html))
            .ToArray();
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

    private static IReadOnlyList<string> SelectSampleUrls(string baseUrl, IEnumerable<string?> candidateUrls, string? preferredUrl, int budget)
    {
        if (budget <= 0)
        {
            return [];
        }

        var selected = new List<string>(budget);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static void AddCandidate(List<string> selectedUrls, HashSet<string> seenUrls, string? absoluteUrl, int maxBudget)
        {
            if (absoluteUrl is null || selectedUrls.Count >= maxBudget || !seenUrls.Add(absoluteUrl))
            {
                return;
            }

            selectedUrls.Add(absoluteUrl);
        }

        AddCandidate(selected, seen, ToAbsoluteUrl(baseUrl, preferredUrl), budget);
        foreach (var candidateUrl in candidateUrls)
        {
            AddCandidate(selected, seen, ToAbsoluteUrl(baseUrl, candidateUrl), budget);
            if (selected.Count >= budget)
            {
                break;
            }
        }

        return selected;
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

    private static IReadOnlyList<string> ExtractProductPageUrls(string baseUrl, IEnumerable<string?> htmlFragments)
    {
        var urls = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

                var absoluteUrl = ToAbsoluteUrl(baseUrl, href);
                if (absoluteUrl is null || !seen.Add(absoluteUrl))
                {
                    continue;
                }

                urls.Add(absoluteUrl);
            }
        }

        return urls;
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

    private decimal ScoreExtractability(bool runtimeExtractionCompatible, bool structuredProductEvidenceDetected, bool technicalAttributeEvidenceDetected, string? representativeProductPageHtml)
    {
        var score = 0m;
        if (!string.IsNullOrWhiteSpace(representativeProductPageHtml))
        {
            score += 10m;
        }

        if (runtimeExtractionCompatible)
        {
            score += 55m;
        }

        if (structuredProductEvidenceDetected)
        {
            score += 20m;
        }

        if (technicalAttributeEvidenceDetected)
        {
            score += 15m;
        }

        return Math.Min(100m, score);
    }

    private int CountRuntimeExtractedProducts(string? html, string? url)
    {
        if (string.IsNullOrWhiteSpace(html) || string.IsNullOrWhiteSpace(url))
        {
            return 0;
        }

        return structuredDataExtractor.ExtractProducts(html, url).Count;
    }

    private ProductSampleEvidence AnalyzeProductSample(string? html, string? url)
    {
        var runtimeProductCount = CountRuntimeExtractedProducts(html, url);
        return new ProductSampleEvidence(
            url,
            html is not null,
            runtimeProductCount,
            HasStructuredProductEvidence(html, url),
            HasTechnicalAttributeEvidence(html));
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

    private async Task<TimedPageClassificationResult> TryClassifyRepresentativeProductPageAsync(string? representativeProductPageHtml, string category, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(representativeProductPageHtml))
        {
            return new TimedPageClassificationResult(null, 0);
        }

        var stopwatch = Stopwatch.StartNew();

        if (!llmOptions.Enabled)
        {
            return new TimedPageClassificationResult(new PageClassificationResult
            {
                IsProductPage = false,
                HasSpecifications = false,
                Confidence = 0d,
                LlmStatus = LlmStatusCodes.Disabled,
                LlmStatusMessage = "LLM validation is disabled for this environment. Set Llm:Enabled=true and configure a local GGUF model to enable it. Discovery uses heuristics only.",
                Reason = "LLM disabled"
            }, stopwatch.ElapsedMilliseconds);
        }

        try
        {
            return new TimedPageClassificationResult(
                await pageClassifier.ClassifyAsync(representativeProductPageHtml, category, cancellationToken),
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Representative product-page classification failed for category {Category} during candidate probing.", category);
            return new TimedPageClassificationResult(new PageClassificationResult
            {
                IsProductPage = false,
                HasSpecifications = false,
                Confidence = 0d,
                LlmStatus = LlmStatusCodes.RuntimeFailed,
                LlmStatusMessage = "LLM validation is configured, but inference failed during this run. Discovery uses heuristics only.",
                Reason = "LLM runtime failed"
            }, stopwatch.ElapsedMilliseconds);
        }
    }

    private static TimeSpan GetRetryDelay(int attempt, int baseDelayMs)
    {
        var safeBaseDelay = Math.Max(50, baseDelayMs);
        var multiplier = Math.Min(8, 1 << Math.Max(0, attempt));
        return TimeSpan.FromMilliseconds(safeBaseDelay * multiplier);
    }

    private static bool IsNeutralLlmResult(PageClassificationResult result)
    {
        return string.Equals(result.Reason, "LLM unconfigured", StringComparison.OrdinalIgnoreCase)
            || string.Equals(result.Reason, "LLM load failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(result.Reason, "LLM runtime failed", StringComparison.OrdinalIgnoreCase)
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

    private static string? ToAbsoluteUrl(string baseUrl, string? candidateUrl)
    {
        if (string.IsNullOrWhiteSpace(candidateUrl))
        {
            return null;
        }

        return Uri.TryCreate(new Uri(baseUrl, UriKind.Absolute), candidateUrl, out var absoluteUri)
            ? absoluteUri.ToString()
            : null;
    }

    private static string? FindFetchedHtml(IEnumerable<FetchedPageSample> samples, string? targetUrl)
    {
        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            return null;
        }

        return samples.FirstOrDefault(sample => string.Equals(sample.Url, targetUrl, StringComparison.OrdinalIgnoreCase))?.Html;
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

    private sealed record FetchedPageSample(string Url, string? Html);

    private sealed record TimedPageClassificationResult(PageClassificationResult? Result, long ElapsedMs);

    private sealed record ProductSampleEvidence(
        string? Url,
        bool PageReachable,
        int RuntimeProductCount,
        bool StructuredProductEvidenceDetected,
        bool TechnicalAttributeEvidenceDetected);
}