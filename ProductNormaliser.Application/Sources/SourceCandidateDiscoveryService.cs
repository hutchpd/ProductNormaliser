using Microsoft.Extensions.Options;
using ProductNormaliser.Application.Categories;
using ProductNormaliser.Application.Governance;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

public sealed class SourceCandidateDiscoveryService(
    ICrawlSourceStore crawlSourceStore,
    ICategoryMetadataService categoryMetadataService,
    ICrawlGovernanceService crawlGovernanceService,
    ISourceCandidateSearchProvider sourceCandidateSearchProvider,
    ISourceCandidateProbeService sourceCandidateProbeService,
    IOptions<SourceOnboardingAutomationOptions>? onboardingAutomationOptions = null) : ISourceCandidateDiscoveryService
{
    private readonly SourceOnboardingAutomationOptions onboardingAutomationOptions = onboardingAutomationOptions?.Value ?? new SourceOnboardingAutomationOptions();

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
            AutomationMode = SourceAutomationModes.Normalize(request.AutomationMode),
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
            var duplicateRiskScore = CalculateDuplicateRiskScore(duplicateSources);
            var heuristicScore = CalculateHeuristicScore(probe, duplicateRiskScore, allowedByGovernance, searchResult, normalizedRequest);
            var llmScore = CalculateLlmScore(probe);
            var confidenceScore = CalculateConfidenceScore(heuristicScore, llmScore, probe);
            var recommendationStatus = DetermineRecommendationStatus(probe, duplicateRiskScore, allowedByGovernance, heuristicScore, llmScore);
            var runtimeExtractionStatus = DetermineRuntimeExtractionStatus(probe);
            var runtimeExtractionMessage = BuildRuntimeExtractionMessage(probe);
            var automationAssessment = BuildAutomationAssessment(searchResult, normalizedRequest, probe, duplicateRiskScore, allowedByGovernance, confidenceScore);
            candidates.Add(new SourceCandidateResult
            {
                CandidateKey = string.IsNullOrWhiteSpace(searchResult.CandidateKey) ? searchResult.Host : searchResult.CandidateKey,
                DisplayName = searchResult.DisplayName,
                BaseUrl = searchResult.BaseUrl,
                Host = searchResult.Host,
                CandidateType = searchResult.CandidateType,
                AllowedMarkets = NormalizeValues(searchResult.AllowedMarkets),
                PreferredLocale = NormalizeOptionalText(searchResult.PreferredLocale),
                MarketEvidence = searchResult.MarketEvidence,
                LocaleEvidence = searchResult.LocaleEvidence,
                ConfidenceScore = confidenceScore,
                CrawlabilityScore = probe.CrawlabilityScore,
                ExtractabilityScore = probe.ExtractabilityScore,
                DuplicateRiskScore = duplicateRiskScore,
                RecommendationStatus = recommendationStatus,
                RuntimeExtractionStatus = runtimeExtractionStatus,
                RuntimeExtractionMessage = runtimeExtractionMessage,
                MatchedCategoryKeys = NormalizeValues(searchResult.MatchedCategoryKeys),
                MatchedBrandHints = NormalizeValues(searchResult.MatchedBrandHints),
                AlreadyRegistered = duplicateSources.Any(),
                DuplicateSourceIds = duplicateSources.Select(source => source.Id).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
                DuplicateSourceDisplayNames = duplicateSources.Select(source => source.DisplayName).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
                AllowedByGovernance = allowedByGovernance,
                GovernanceWarning = governanceWarning,
                Probe = probe,
                AutomationAssessment = automationAssessment,
                Reasons = reasons
            });
        }

        return new SourceCandidateDiscoveryResult
        {
            RequestedCategoryKeys = categoryKeys,
            Locale = normalizedRequest.Locale,
            Market = normalizedRequest.Market,
            AutomationMode = normalizedRequest.AutomationMode ?? SourceAutomationModes.OperatorAssisted,
            BrandHints = NormalizeValues(normalizedRequest.BrandHints),
            GeneratedUtc = DateTime.UtcNow,
            Candidates = candidates
                .OrderByDescending(candidate => ScoreMarketAlignment(candidate, normalizedRequest))
                .ThenByDescending(candidate => candidate.ConfidenceScore)
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
                AllowedMarkets = ordered.SelectMany(candidate => candidate.AllowedMarkets)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                PreferredLocale = ordered.Select(candidate => candidate.PreferredLocale)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                MarketEvidence = ordered
                    .Select(candidate => candidate.MarketEvidence)
                    .FirstOrDefault(value => GetEvidenceStrength(value) == ordered.Max(item => GetEvidenceStrength(item.MarketEvidence))) ?? "missing",
                LocaleEvidence = ordered
                    .Select(candidate => candidate.LocaleEvidence)
                    .FirstOrDefault(value => GetEvidenceStrength(value) == ordered.Max(item => GetEvidenceStrength(item.LocaleEvidence))) ?? "missing",
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

        if (probe.RepresentativeCategoryPageReachable)
        {
            reasons.Add(new SourceCandidateReason
            {
                Code = "category_page",
                Message = "A representative category page was reachable during probing.",
                Weight = 18m
            });
        }

        if (probe.RepresentativeProductPageReachable)
        {
            reasons.Add(new SourceCandidateReason
            {
                Code = "product_page",
                Message = "A representative product page was reachable during probing.",
                Weight = 22m
            });
        }

        if (probe.StructuredProductEvidenceDetected)
        {
            reasons.Add(new SourceCandidateReason
            {
                Code = "structured_product_evidence",
                Message = "Structured product evidence was detected on a representative product page.",
                Weight = 32m
            });
        }

        if (probe.TechnicalAttributeEvidenceDetected)
        {
            reasons.Add(new SourceCandidateReason
            {
                Code = "technical_attribute_evidence",
                Message = "Technical attribute evidence was detected even without relying only on JSON-LD.",
                Weight = 24m
            });
        }

        if (probe.RuntimeExtractionCompatible)
        {
            reasons.Add(new SourceCandidateReason
            {
                Code = "runtime_extraction_compatible",
                Message = probe.RepresentativeRuntimeProductCount == 1
                    ? "The representative product page produced 1 product through the live runtime extractor."
                    : $"The representative product page produced {probe.RepresentativeRuntimeProductCount} products through the live runtime extractor.",
                Weight = 40m
            });
        }
        else if (probe.RepresentativeProductPageReachable)
        {
            reasons.Add(new SourceCandidateReason
            {
                Code = "runtime_extraction_mismatch",
                Message = "The representative product page was reachable, but the live runtime extractor did not produce any products from it.",
                Weight = -34m
            });
        }

        if (probe.LlmAcceptedRepresentativeProductPage)
        {
            reasons.Add(new SourceCandidateReason
            {
                Code = "llm_validated_product_page",
                Message = "The LLM agreed that the representative product page looks like a real product page with specifications.",
                Weight = 16m
            });
        }

        if (probe.LlmRejectedRepresentativeProductPage)
        {
            reasons.Add(new SourceCandidateReason
            {
                Code = "llm_rejected_product_page",
                Message = "LLM rejected the representative product page as a reliable product/specification page.",
                Weight = -35m
            });
        }

        if (!string.IsNullOrWhiteSpace(probe.LlmReason)
            && (probe.LlmReason.Contains("unavailable", StringComparison.OrdinalIgnoreCase)
                || probe.LlmReason.Contains("timeout", StringComparison.OrdinalIgnoreCase)
                || probe.LlmReason.Contains("low confidence", StringComparison.OrdinalIgnoreCase)
                || probe.LlmReason.Contains("disabled", StringComparison.OrdinalIgnoreCase)))
        {
            reasons.Add(new SourceCandidateReason
            {
                Code = "llm_neutral",
                Message = probe.LlmReason,
                Weight = 0m
            });
        }

        if (probe.LlmDisagreedWithHeuristics)
        {
            reasons.Add(new SourceCandidateReason
            {
                Code = "llm_disagreement",
                Message = "Heuristic extraction signals and the LLM classification disagree, so this candidate needs manual review.",
                Weight = -18m
            });
        }

        if (probe.NonCatalogContentHeavy)
        {
            reasons.Add(new SourceCandidateReason
            {
                Code = "non_catalog_bias",
                Message = "The sampled pages look more support, blog, or marketing heavy than product-catalog heavy.",
                Weight = -28m
            });
        }

        if (probe.RepresentativeProductPageReachable
            && !probe.StructuredProductEvidenceDetected
            && !probe.TechnicalAttributeEvidenceDetected)
        {
            reasons.Add(new SourceCandidateReason
            {
                Code = "weak_extractability",
                Message = "Representative product pages were reachable but did not show useful product or technical-attribute evidence.",
                Weight = -30m
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

    private SourceCandidateAutomationAssessment BuildAutomationAssessment(
        SourceCandidateSearchResult searchResult,
        DiscoverSourceCandidatesRequest request,
        SourceCandidateProbeResult probe,
        decimal duplicateRiskScore,
        bool allowedByGovernance,
        decimal confidenceScore)
    {
        var requestedMode = SourceAutomationModes.Normalize(request.AutomationMode);
        var requestedMarket = NormalizeOptionalText(request.Market);
        var requestedLocale = NormalizeOptionalText(request.Locale);
        var candidateMarkets = NormalizeValues(searchResult.AllowedMarkets);

        var marketMatchApproved = !string.IsNullOrWhiteSpace(requestedMarket)
            && candidateMarkets.Count > 0
            && candidateMarkets.Contains(requestedMarket, StringComparer.OrdinalIgnoreCase);
        var marketEvidenceStrongEnough = string.Equals(searchResult.MarketEvidence, "explicit", StringComparison.OrdinalIgnoreCase)
            && candidateMarkets.Count == 1
            && marketMatchApproved;
        var duplicateRiskAccepted = duplicateRiskScore <= onboardingAutomationOptions.MaxDuplicateRiskScore;
        var representativeValidationPassed = probe.RepresentativeCategoryPageReachable
            && probe.RepresentativeProductPageReachable;
        var extractabilityConfidencePassed = probe.RuntimeExtractionCompatible
            && probe.ExtractabilityScore >= onboardingAutomationOptions.MinExtractabilityScore;
        var yieldConfidenceScore = CalculateYieldConfidenceScore(probe);
        var yieldConfidencePassed = yieldConfidenceScore >= onboardingAutomationOptions.MinYieldConfidenceScore;
        var localeAligned = string.IsNullOrWhiteSpace(requestedLocale)
            || string.Equals(NormalizeOptionalText(searchResult.PreferredLocale), requestedLocale, StringComparison.OrdinalIgnoreCase);

        var baseGuardrailsPassed = marketMatchApproved
            && marketEvidenceStrongEnough
            && allowedByGovernance
            && duplicateRiskAccepted
            && representativeValidationPassed
            && extractabilityConfidencePassed
            && yieldConfidencePassed
            && localeAligned
            && probe.CrawlabilityScore >= onboardingAutomationOptions.MinCrawlabilityScore
            && probe.CategoryRelevanceScore >= onboardingAutomationOptions.MinCategoryRelevanceScore
            && probe.CatalogLikelihoodScore >= onboardingAutomationOptions.MinCatalogLikelihoodScore;

        var eligibleForSuggestion = requestedMode is SourceAutomationModes.SuggestAccept or SourceAutomationModes.AutoAcceptAndSeed
            && baseGuardrailsPassed
            && confidenceScore >= onboardingAutomationOptions.SuggestMinConfidenceScore;
        var eligibleForAutoAccept = requestedMode == SourceAutomationModes.AutoAcceptAndSeed
            && eligibleForSuggestion
            && confidenceScore >= onboardingAutomationOptions.AutoAcceptMinConfidenceScore;

        var supportingReasons = new List<string>();
        if (marketMatchApproved)
        {
            supportingReasons.Add($"Requested market '{requestedMarket}' matches candidate market metadata.");
        }

        if (marketEvidenceStrongEnough)
        {
            supportingReasons.Add("Market evidence is explicit rather than only request-hinted.");
        }

        if (representativeValidationPassed)
        {
            supportingReasons.Add("Representative category and product pages were both validated.");
        }

        if (extractabilityConfidencePassed)
        {
            supportingReasons.Add("Representative product evidence cleared the extractability threshold through the live runtime extractor.");
        }

        if (yieldConfidencePassed)
        {
            supportingReasons.Add($"Predicted downstream yield confidence scored {yieldConfidenceScore:0.#}.");
        }

        var blockingReasons = new List<string>();
        if (string.IsNullOrWhiteSpace(requestedMarket))
        {
            blockingReasons.Add("Automation requires an explicit requested market so source policy stays operator-scoped.");
        }

        if (!marketMatchApproved)
        {
            blockingReasons.Add("Candidate market metadata does not clearly match the requested market.");
        }

        if (!marketEvidenceStrongEnough)
        {
            blockingReasons.Add("Candidate market metadata is missing, weakly inferred, or regionally ambiguous.");
        }

        if (!allowedByGovernance)
        {
            blockingReasons.Add("Governance rejected this candidate.");
        }

        if (!duplicateRiskAccepted)
        {
            blockingReasons.Add("Duplicate risk is too high for automation.");
        }

        if (!representativeValidationPassed)
        {
            blockingReasons.Add("Representative category and product validation both need to succeed before automation.");
        }

        if (!extractabilityConfidencePassed)
        {
            blockingReasons.Add("Representative product validation did not produce products through the live runtime extractor.");
        }

        if (!yieldConfidencePassed)
        {
            blockingReasons.Add("Predicted downstream yield confidence is below the guarded threshold.");
        }

        if (!localeAligned)
        {
            blockingReasons.Add("Candidate locale does not align cleanly with the requested locale.");
        }

        if (probe.CrawlabilityScore < onboardingAutomationOptions.MinCrawlabilityScore)
        {
            blockingReasons.Add("Crawlability is below the guarded threshold.");
        }

        if (probe.CategoryRelevanceScore < onboardingAutomationOptions.MinCategoryRelevanceScore)
        {
            blockingReasons.Add("Category relevance is below the guarded threshold.");
        }

        if (probe.CatalogLikelihoodScore < onboardingAutomationOptions.MinCatalogLikelihoodScore)
        {
            blockingReasons.Add("Catalog-likelihood is below the guarded threshold.");
        }

        if (confidenceScore < onboardingAutomationOptions.SuggestMinConfidenceScore)
        {
            blockingReasons.Add("Overall confidence is below the suggestion threshold.");
        }

        if (requestedMode == SourceAutomationModes.AutoAcceptAndSeed && confidenceScore < onboardingAutomationOptions.AutoAcceptMinConfidenceScore)
        {
            blockingReasons.Add("Overall confidence is below the auto-accept threshold.");
        }

        var decision = eligibleForAutoAccept
            ? SourceCandidateAutomationAssessment.DecisionAutoAcceptAndSeed
            : eligibleForSuggestion
                ? SourceCandidateAutomationAssessment.DecisionSuggestAccept
                : SourceCandidateAutomationAssessment.DecisionManualOnly;

        return new SourceCandidateAutomationAssessment
        {
            RequestedMode = requestedMode,
            Decision = decision,
            MarketMatchApproved = marketMatchApproved,
            MarketEvidenceStrongEnough = marketEvidenceStrongEnough,
            GovernancePassed = allowedByGovernance,
            DuplicateRiskAccepted = duplicateRiskAccepted,
            RepresentativeValidationPassed = representativeValidationPassed,
            ExtractabilityConfidencePassed = extractabilityConfidencePassed,
            YieldConfidencePassed = yieldConfidencePassed,
            EligibleForSuggestion = eligibleForSuggestion,
            EligibleForAutoAccept = eligibleForAutoAccept,
            EligibleForAutoSeed = eligibleForAutoAccept,
            MarketEvidence = searchResult.MarketEvidence,
            LocaleEvidence = searchResult.LocaleEvidence,
            SupportingReasons = supportingReasons,
            BlockingReasons = blockingReasons.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private static decimal CalculateHeuristicScore(SourceCandidateProbeResult probe, decimal duplicateRiskScore, bool allowedByGovernance, SourceCandidateSearchResult searchResult, DiscoverSourceCandidatesRequest request)
    {
        var score = probe.CrawlabilityScore * 0.30m
            + probe.CategoryRelevanceScore * 0.25m
            + GetHeuristicExtractabilityScore(probe) * 0.35m
            + probe.CatalogLikelihoodScore * 0.10m
            - (duplicateRiskScore * 0.35m);

        if (probe.RuntimeExtractionCompatible)
        {
            score += 10m;
        }
        else if (probe.RepresentativeProductPageReachable)
        {
            score -= 25m;
        }

        score += ScoreMarketAlignment(searchResult, request);

        if (!allowedByGovernance)
        {
            score = Math.Min(score, 10m);
        }

        return Math.Clamp(decimal.Round(score, 2, MidpointRounding.AwayFromZero), 0m, 100m);
    }

    private static decimal CalculateLlmScore(SourceCandidateProbeResult probe)
    {
        if (probe.LlmAcceptedRepresentativeProductPage)
        {
            return probe.LlmConfidenceScore ?? 80m;
        }

        if (probe.LlmRejectedRepresentativeProductPage)
        {
            return probe.LlmConfidenceScore ?? 20m;
        }

        return -1m;
    }

    private static decimal CalculateConfidenceScore(decimal heuristicScore, decimal llmScore, SourceCandidateProbeResult probe)
    {
        var score = llmScore < 0m
            ? heuristicScore
            : (heuristicScore * 0.6m) + (llmScore * 0.4m);

        if (probe.LlmDisagreedWithHeuristics)
        {
            score = Math.Min(score, 69.99m);
        }

        return Math.Clamp(decimal.Round(score, 2, MidpointRounding.AwayFromZero), 0m, 100m);
    }

    private static decimal CalculateDuplicateRiskScore(IReadOnlyCollection<Core.Models.CrawlSource> duplicateSources)
    {
        if (duplicateSources.Count == 0)
        {
            return 0m;
        }

        return Math.Min(100m, 60m + ((duplicateSources.Count - 1) * 20m));
    }

    private static decimal CalculateYieldConfidenceScore(SourceCandidateProbeResult probe)
    {
        var score = 0m;

        if (probe.RepresentativeProductPageReachable)
        {
            score += 20m;
        }

        if (probe.RuntimeExtractionCompatible)
        {
            score += 35m;
        }
        else if (probe.StructuredProductEvidenceDetected)
        {
            score += 10m;
        }

        if (probe.TechnicalAttributeEvidenceDetected)
        {
            score += 5m;
        }

        if (probe.SitemapDetected)
        {
            score += 10m;
        }

        if (probe.LikelyListingUrlPatterns.Count > 0)
        {
            score += 10m;
        }

        if (probe.CatalogLikelihoodScore >= 60m)
        {
            score += 5m;
        }

        return Math.Clamp(decimal.Round(score, 2, MidpointRounding.AwayFromZero), 0m, 100m);
    }

    private static string DetermineRecommendationStatus(SourceCandidateProbeResult probe, decimal duplicateRiskScore, bool allowedByGovernance, decimal heuristicScore, decimal llmScore)
    {
        if (!allowedByGovernance)
        {
            return SourceCandidateResult.RecommendationDoNotAccept;
        }

        if (probe.RuntimeExtractionCompatible)
        {
            return probe.CrawlabilityScore >= 55m
                && probe.CategoryRelevanceScore >= 35m
                && probe.ExtractabilityScore >= 55m
                && probe.CatalogLikelihoodScore >= 45m
                && duplicateRiskScore < 50m
                    ? SourceCandidateResult.RecommendationRecommended
                    : SourceCandidateResult.RecommendationManualReview;
        }

        var heuristicsStrong = heuristicScore >= 70m;
        var heuristicsWeak = heuristicScore < 45m;
        var llmAccepted = probe.LlmAcceptedRepresentativeProductPage;
        var llmRejected = probe.LlmRejectedRepresentativeProductPage;
        var hasLlmSignal = llmScore >= 0m;

        if (hasLlmSignal)
        {
            if (heuristicsStrong && llmAccepted)
            {
                return duplicateRiskScore < 50m
                    ? SourceCandidateResult.RecommendationRecommended
                    : SourceCandidateResult.RecommendationManualReview;
            }

            if (heuristicsWeak && llmRejected)
            {
                return SourceCandidateResult.RecommendationDoNotAccept;
            }
        }

        if (probe.LlmDisagreedWithHeuristics)
        {
            return SourceCandidateResult.RecommendationManualReview;
        }

        if (probe.LlmRejectedRepresentativeProductPage)
        {
            return SourceCandidateResult.RecommendationDoNotAccept;
        }

        if (probe.RepresentativeProductPageReachable && !probe.RuntimeExtractionCompatible)
        {
            return probe.StructuredProductEvidenceDetected || probe.TechnicalAttributeEvidenceDetected
                ? SourceCandidateResult.RecommendationManualReview
                : SourceCandidateResult.RecommendationDoNotAccept;
        }

        if (probe.RepresentativeProductPageReachable
            && !probe.StructuredProductEvidenceDetected
            && !probe.TechnicalAttributeEvidenceDetected)
        {
            return SourceCandidateResult.RecommendationDoNotAccept;
        }

        if (probe.CatalogLikelihoodScore < 25m && probe.CategoryRelevanceScore < 25m)
        {
            return SourceCandidateResult.RecommendationDoNotAccept;
        }

        if (probe.CrawlabilityScore >= 55m
            && probe.CategoryRelevanceScore >= 35m
            && probe.ExtractabilityScore >= 55m
            && probe.CatalogLikelihoodScore >= 45m
            && duplicateRiskScore < 50m)
        {
            return SourceCandidateResult.RecommendationRecommended;
        }

        return SourceCandidateResult.RecommendationManualReview;
    }

    private static string DetermineRuntimeExtractionStatus(SourceCandidateProbeResult probe)
    {
        if (probe.RuntimeExtractionCompatible)
        {
            return SourceCandidateResult.RuntimeExtractionCompatibleStatus;
        }

        if (probe.RepresentativeProductPageReachable)
        {
            return SourceCandidateResult.RuntimeExtractionNotCompatibleStatus;
        }

        return SourceCandidateResult.RuntimeExtractionManualReviewOnlyStatus;
    }

    private static string BuildRuntimeExtractionMessage(SourceCandidateProbeResult probe)
    {
        if (probe.RuntimeExtractionCompatible)
        {
            return probe.RepresentativeRuntimeProductCount == 1
                ? "Representative runtime extraction succeeded on the sampled product page."
                : $"Representative runtime extraction succeeded on the sampled product page and produced {probe.RepresentativeRuntimeProductCount} products.";
        }

        if (probe.RepresentativeProductPageReachable)
        {
            return "Representative runtime extraction did not produce products from the sampled product page.";
        }

        return "Representative runtime extraction could not be confirmed from the sampled pages, so this candidate needs manual review.";
    }

    private static decimal GetHeuristicExtractabilityScore(SourceCandidateProbeResult probe)
    {
        return probe.HeuristicExtractabilityScore > 0m || probe.ExtractabilityScore == 0m
            ? probe.HeuristicExtractabilityScore
            : probe.ExtractabilityScore;
    }

    private static bool IsPotentialDuplicate(Core.Models.CrawlSource source, SourceCandidateSearchResult candidate)
    {
        if (string.Equals(NormalizeHost(source.Host), NormalizeHost(candidate.Host), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(source.DisplayName)
            && string.Equals(NormalizeName(source.DisplayName), NormalizeName(candidate.DisplayName), StringComparison.OrdinalIgnoreCase)
            && ShareAnyMarket(source.AllowedMarkets, candidate.AllowedMarkets))
        {
            return true;
        }

        if (string.Equals(NormalizeBaseUrl(source.BaseUrl), NormalizeBaseUrl(candidate.BaseUrl), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var sourceMarkets = NormalizeValues(source.AllowedMarkets);
        var candidateMarkets = NormalizeValues(candidate.AllowedMarkets);
        if (sourceMarkets.Count != 0 && candidateMarkets.Count != 0)
        {
            return false;
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

    private static bool ShareAnyMarket(IReadOnlyCollection<string> sourceMarkets, IReadOnlyCollection<string> candidateMarkets)
    {
        var normalizedSourceMarkets = NormalizeValues(sourceMarkets);
        var normalizedCandidateMarkets = NormalizeValues(candidateMarkets);
        if (normalizedSourceMarkets.Count == 0 || normalizedCandidateMarkets.Count == 0)
        {
            return false;
        }

        return normalizedSourceMarkets.Any(market => normalizedCandidateMarkets.Contains(market, StringComparer.OrdinalIgnoreCase));
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

    private static decimal ScoreMarketAlignment(SourceCandidateResult candidate, DiscoverSourceCandidatesRequest request)
    {
        return ScoreMarketAlignment(new SourceCandidateSearchResult
        {
            AllowedMarkets = candidate.AllowedMarkets,
            PreferredLocale = candidate.PreferredLocale
        }, request);
    }

    private static decimal ScoreMarketAlignment(SourceCandidateSearchResult candidate, DiscoverSourceCandidatesRequest request)
    {
        var score = 0m;
        var candidateMarkets = NormalizeValues(candidate.AllowedMarkets);
        var requestedMarket = NormalizeOptionalText(request.Market);
        if (!string.IsNullOrWhiteSpace(requestedMarket))
        {
            if (candidateMarkets.Contains(requestedMarket, StringComparer.OrdinalIgnoreCase))
            {
                score += 8m;
            }
            else if (candidateMarkets.Count > 0)
            {
                score -= 12m;
            }
        }

        var preferredLocale = NormalizeOptionalText(candidate.PreferredLocale);
        if (!string.IsNullOrWhiteSpace(request.Locale) && !string.IsNullOrWhiteSpace(preferredLocale))
        {
            score += string.Equals(preferredLocale, request.Locale, StringComparison.OrdinalIgnoreCase) ? 4m : -6m;
        }

        return score;
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