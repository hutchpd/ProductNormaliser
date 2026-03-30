using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

internal sealed class SourceCandidateDiscoveryEvaluator(SourceOnboardingAutomationOptions onboardingAutomationOptions)
{
    public IReadOnlyList<SourceCandidateSearchResult> CollapseEquivalentCandidates(IReadOnlyList<SourceCandidateSearchResult> candidates)
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

    public SourceCandidateResult BuildCandidateResult(
        SourceCandidateSearchResult searchResult,
        DiscoverSourceCandidatesRequest request,
        SourceCandidateProbeResult probe,
        IReadOnlyCollection<CrawlSource> duplicateSources,
        string? governanceWarning,
        bool allowedByGovernance)
    {
        var reasons = BuildReasons(searchResult, probe, duplicateSources, governanceWarning);
        var duplicateRiskScore = CalculateDuplicateRiskScore(duplicateSources);
        var heuristicScore = CalculateHeuristicScore(probe, duplicateRiskScore, allowedByGovernance, searchResult, request);
        var llmScore = CalculateLlmScore(probe);
        var confidenceScore = CalculateConfidenceScore(heuristicScore, llmScore, probe);
        var recommendationStatus = DetermineRecommendationStatus(probe, duplicateRiskScore, allowedByGovernance, heuristicScore, llmScore);
        var runtimeExtractionStatus = DetermineRuntimeExtractionStatus(probe);
        var runtimeExtractionMessage = BuildRuntimeExtractionMessage(probe);
        var automationAssessment = BuildAutomationAssessment(searchResult, request, probe, duplicateRiskScore, allowedByGovernance, confidenceScore);

        return new SourceCandidateResult
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
        };
    }

    public IReadOnlyList<SourceCandidateDiscoveryDiagnostic> BuildProbeDiagnostics(IReadOnlyCollection<SourceCandidateResult> candidates)
    {
        var diagnostics = new List<SourceCandidateDiscoveryDiagnostic>();

        var timedOutCandidates = candidates
            .Where(candidate => candidate.Probe.ProbeTimedOut)
            .ToArray();
        if (timedOutCandidates.Length > 0)
        {
            var timeoutAverageMs = GetAverageDurationMs(timedOutCandidates.Select(candidate => (long?)candidate.Probe.ProbeElapsedMs));
            diagnostics.Add(new SourceCandidateDiscoveryDiagnostic
            {
                Code = "probe_timeout",
                Severity = SourceCandidateDiscoveryDiagnostic.SeverityWarning,
                Title = "Candidate probing timed out",
                Message = timeoutAverageMs is null
                    ? $"The per-candidate representative-page probe budget was exceeded for {timedOutCandidates.Length} candidate(s). Discovery continued with reduced confidence for those hosts."
                    : $"The per-candidate representative-page probe budget was exceeded for {timedOutCandidates.Length} candidate(s), timing out at about {FormatDuration(timeoutAverageMs.Value)} per candidate. Discovery continued with reduced confidence for those hosts."
            });
        }

        var failedCandidates = candidates.Count(candidate => candidate.Probe.ProbeFailed);
        if (failedCandidates > 0)
        {
            diagnostics.Add(new SourceCandidateDiscoveryDiagnostic
            {
                Code = "probe_failed",
                Severity = SourceCandidateDiscoveryDiagnostic.SeverityWarning,
                Title = "Candidate probing failed",
                Message = $"Representative-page probing failed unexpectedly for {failedCandidates} candidate(s). Discovery continued with reduced confidence for those hosts."
            });
        }

        var categoryFetchFailures = candidates.Count(candidate => candidate.Probe.RepresentativeCategoryPageFetchFailed);
        var productFetchFailures = candidates.Count(candidate => candidate.Probe.RepresentativeProductPageFetchFailed);
        var affectedFetchFailureCandidates = candidates.Count(candidate => candidate.Probe.RepresentativeCategoryPageFetchFailed || candidate.Probe.RepresentativeProductPageFetchFailed);
        if (affectedFetchFailureCandidates > 0)
        {
            diagnostics.Add(new SourceCandidateDiscoveryDiagnostic
            {
                Code = "representative_page_fetch_failed",
                Severity = SourceCandidateDiscoveryDiagnostic.SeverityWarning,
                Title = "Representative page fetch failed",
                Message = $"Representative pages could not be fetched for {affectedFetchFailureCandidates} candidate(s): {categoryFetchFailures} category page failure(s) and {productFetchFailures} product page failure(s). Discovery still scored those hosts, but runtime extraction evidence is incomplete."
            });
        }

        var retriedCandidates = candidates.Count(candidate => candidate.Probe.ProbeAttemptCount > 1);
        if (retriedCandidates > 0)
        {
            diagnostics.Add(new SourceCandidateDiscoveryDiagnostic
            {
                Code = "probe_retried",
                Severity = SourceCandidateDiscoveryDiagnostic.SeverityInfo,
                Title = "Candidate probing retried",
                Message = $"Transient probe slowdowns were retried with exponential backoff for {retriedCandidates} candidate(s) before final scoring."
            });
        }

        return diagnostics;
    }

    public IReadOnlyList<SourceCandidateDiscoveryDiagnostic> BuildLlmDiagnostics(IReadOnlyCollection<SourceCandidateResult> candidates)
    {
        var diagnostics = new List<SourceCandidateDiscoveryDiagnostic>();
        var neutralReasons = candidates
            .Select(candidate => NormalizeNeutralLlmReason(candidate.Probe.LlmReason))
            .Where(reason => reason is not null)
            .GroupBy(reason => reason!, StringComparer.OrdinalIgnoreCase);

        foreach (var group in neutralReasons)
        {
            var count = group.Count();
            switch (group.Key)
            {
                case "LLM unavailable":
                    diagnostics.Add(new SourceCandidateDiscoveryDiagnostic
                    {
                        Code = "llm_unavailable",
                        Severity = SourceCandidateDiscoveryDiagnostic.SeverityWarning,
                        Title = "LLM validation unavailable",
                        Message = $"Representative product-page classification fell back to heuristics for {count} candidate(s) because the LLM was unavailable."
                    });
                    break;

                case "LLM unconfigured":
                    diagnostics.Add(new SourceCandidateDiscoveryDiagnostic
                    {
                        Code = "llm_unconfigured",
                        Severity = SourceCandidateDiscoveryDiagnostic.SeverityInfo,
                        Title = "LLM validation not configured locally",
                        Message = $"Representative product-page classification stayed heuristic-only for {count} candidate(s) because no local GGUF model is configured or present."
                    });
                    break;

                case "LLM load failed":
                    diagnostics.Add(new SourceCandidateDiscoveryDiagnostic
                    {
                        Code = "llm_load_failed",
                        Severity = SourceCandidateDiscoveryDiagnostic.SeverityWarning,
                        Title = "LLM model failed to load",
                        Message = $"Representative product-page classification fell back to heuristics for {count} candidate(s) because the configured local model could not be loaded."
                    });
                    break;

                case "LLM runtime failed":
                    diagnostics.Add(new SourceCandidateDiscoveryDiagnostic
                    {
                        Code = "llm_runtime_failed",
                        Severity = SourceCandidateDiscoveryDiagnostic.SeverityWarning,
                        Title = "LLM validation failed during probing",
                        Message = $"Representative product-page classification fell back to heuristics for {count} candidate(s) because inference failed during the discovery run."
                    });
                    break;

                case "LLM timeout":
                    var timeoutCandidates = candidates
                        .Where(candidate => candidate.Probe.LlmTimedOut
                            || string.Equals(NormalizeNeutralLlmReason(candidate.Probe.LlmReason), group.Key, StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                    var timeoutAverageMs = GetAverageDurationMs(timeoutCandidates.Select(candidate => candidate.Probe.LlmElapsedMs));
                    diagnostics.Add(new SourceCandidateDiscoveryDiagnostic
                    {
                        Code = "llm_timeout",
                        Severity = SourceCandidateDiscoveryDiagnostic.SeverityWarning,
                        Title = "LLM validation timed out",
                        Message = timeoutAverageMs is null
                            ? $"Representative product-page classification timed out for {count} candidate(s). Search and page probing still completed, but those hosts were scored with heuristics only."
                            : $"Representative product-page classification timed out for {count} candidate(s) after about {FormatDuration(timeoutAverageMs.Value)} per candidate. Search and page probing still completed, but those hosts were scored with heuristics only."
                    });
                    break;

                case "LLM low confidence":
                    diagnostics.Add(new SourceCandidateDiscoveryDiagnostic
                    {
                        Code = "llm_low_confidence",
                        Severity = SourceCandidateDiscoveryDiagnostic.SeverityInfo,
                        Title = "LLM stayed neutral",
                        Message = $"Representative product-page classification stayed heuristic-only for {count} candidate(s) because the LLM did not reach its confidence threshold."
                    });
                    break;

                case "LLM disabled":
                    diagnostics.Add(new SourceCandidateDiscoveryDiagnostic
                    {
                        Code = "llm_disabled",
                        Severity = SourceCandidateDiscoveryDiagnostic.SeverityInfo,
                        Title = "LLM validation disabled",
                        Message = $"Representative product-page classification stayed heuristic-only for {count} candidate(s) because LLM evaluation is currently disabled."
                    });
                    break;
            }
        }

        var llmMeasuredCandidates = candidates
            .Where(candidate => candidate.Probe.LlmElapsedMs is > 0)
            .ToArray();
        if (llmMeasuredCandidates.Length > 0)
        {
            var totalElapsedMs = llmMeasuredCandidates.Sum(candidate => candidate.Probe.LlmElapsedMs ?? 0);
            var averageElapsedMs = (long)Math.Round(totalElapsedMs / (double)llmMeasuredCandidates.Length, MidpointRounding.AwayFromZero);
            diagnostics.Add(new SourceCandidateDiscoveryDiagnostic
            {
                Code = "llm_throughput",
                Severity = SourceCandidateDiscoveryDiagnostic.SeverityInfo,
                Title = "Local LLM verification throughput",
                Message = $"Representative product-page validation processed {llmMeasuredCandidates.Length} candidate(s) in {FormatDuration(totalElapsedMs)} total, averaging {FormatDuration(averageElapsedMs)} per candidate. Local verification runs serially so the model can keep up."
            });
        }

        return diagnostics;
    }

    public IReadOnlyList<SourceCandidateResult> OrderCandidates(IReadOnlyList<SourceCandidateResult> candidates, DiscoverSourceCandidatesRequest request)
    {
        return candidates
            .OrderByDescending(candidate => ScoreMarketAlignment(candidate, request))
            .ThenByDescending(candidate => candidate.ConfidenceScore)
            .ThenBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(NormalizeMaxCandidates(request.MaxCandidates))
            .ToArray();
    }

    public static List<string> NormalizeValues(IEnumerable<string>? values)
    {
        return (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static int NormalizeMaxCandidates(int value)
    {
        if (value <= 0)
        {
            return 10;
        }

        return Math.Min(25, value);
    }

    public bool IsPotentialDuplicate(CrawlSource source, SourceCandidateSearchResult candidate)
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

    private static long? GetAverageDurationMs(IEnumerable<long?> durations)
    {
        var measured = durations
            .Where(value => value is > 0)
            .Select(value => value!.Value)
            .ToArray();

        return measured.Length == 0 ? null : (long)Math.Round(measured.Average(), MidpointRounding.AwayFromZero);
    }

    private static string FormatDuration(long durationMs)
    {
        return durationMs >= 1000
            ? $"{durationMs / 1000d:0.#}s"
            : $"{durationMs}ms";
    }

    private static string? NormalizeNeutralLlmReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        return reason.Trim() switch
        {
            "LLM unconfigured" => "LLM unconfigured",
            "LLM load failed" => "LLM load failed",
            "LLM runtime failed" => "LLM runtime failed",
            "LLM unavailable" => "LLM unavailable",
            "LLM timeout" => "LLM timeout",
            "LLM low confidence" => "LLM low confidence",
            "LLM disabled" => "LLM disabled",
            _ => null
        };
    }

    private static IReadOnlyList<SourceCandidateReason> BuildReasons(SourceCandidateSearchResult searchResult, SourceCandidateProbeResult probe, IReadOnlyCollection<CrawlSource> duplicateSources, string? governanceWarning)
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
        else if (probe.RepresentativeCategoryPageFetchFailed)
        {
            reasons.Add(new SourceCandidateReason
            {
                Code = "category_page_fetch_failed",
                Message = "A representative category page was selected, but it could not be fetched during probing.",
                Weight = -12m
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
        else if (probe.RepresentativeProductPageFetchFailed)
        {
            reasons.Add(new SourceCandidateReason
            {
                Code = "product_page_fetch_failed",
                Message = "A representative product page was selected, but it could not be fetched during probing.",
                Weight = -16m
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

        if (probe.ProbeTimedOut)
        {
            reasons.Add(new SourceCandidateReason
            {
                Code = "probe_timeout",
                Message = "Representative-page probing exceeded the per-candidate probe budget before runtime evidence could be completed.",
                Weight = -22m
            });
        }

        if (probe.ProbeFailed)
        {
            reasons.Add(new SourceCandidateReason
            {
                Code = "probe_failed",
                Message = "Representative-page probing failed before runtime evidence could be completed.",
                Weight = -24m
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

    private SourceCandidateAutomationAssessment BuildAutomationAssessment(SourceCandidateSearchResult searchResult, DiscoverSourceCandidatesRequest request, SourceCandidateProbeResult probe, decimal duplicateRiskScore, bool allowedByGovernance, decimal confidenceScore)
    {
        var requestedMode = SourceAutomationModes.Normalize(request.AutomationMode);
        var requestedMarket = NormalizeOptionalText(request.Market);
        var requestedLocale = NormalizeOptionalText(request.Locale);
        var candidateMarkets = NormalizeValues(searchResult.AllowedMarkets);
        var candidateMarketList = FormatValueList(candidateMarkets, "none");
        var unattendedAutomationRequested = requestedMode is SourceAutomationModes.SuggestAccept or SourceAutomationModes.AutoAcceptAndSeed;

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
        var suggestionBreadthPassed = HasSuggestionAutomationEvidence(probe, onboardingAutomationOptions);
        var autoAcceptBreadthPassed = HasAutoAcceptAutomationEvidence(probe, onboardingAutomationOptions);
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
            && suggestionBreadthPassed
            && confidenceScore >= onboardingAutomationOptions.SuggestMinConfidenceScore;
        var eligibleForAutoAccept = requestedMode == SourceAutomationModes.AutoAcceptAndSeed
            && eligibleForSuggestion
            && autoAcceptBreadthPassed
            && confidenceScore >= onboardingAutomationOptions.AutoAcceptMinConfidenceScore;

        var supportingReasons = new List<string>();
        if (marketMatchApproved)
        {
            supportingReasons.Add($"Requested market '{requestedMarket}' matches candidate market metadata ({candidateMarketList}).");
        }

        if (marketEvidenceStrongEnough)
        {
            supportingReasons.Add($"Market evidence is explicit and scoped to a single market ({candidateMarketList}).");
        }

        if (allowedByGovernance)
        {
            supportingReasons.Add("Governance allowed this candidate for discovery publication.");
        }

        if (duplicateRiskAccepted)
        {
            supportingReasons.Add($"Duplicate risk scored {duplicateRiskScore:0.#} against the guarded maximum {onboardingAutomationOptions.MaxDuplicateRiskScore:0.#}.");
        }

        if (representativeValidationPassed)
        {
            supportingReasons.Add("Representative category and product pages were both validated.");
        }

        if (extractabilityConfidencePassed)
        {
            supportingReasons.Add($"Representative product evidence cleared extractability at {probe.ExtractabilityScore:0.#} against {onboardingAutomationOptions.MinExtractabilityScore:0.#} through the live runtime extractor.");
        }

        if (yieldConfidencePassed)
        {
            supportingReasons.Add($"Predicted downstream yield confidence scored {yieldConfidenceScore:0.#} against {onboardingAutomationOptions.MinYieldConfidenceScore:0.#}.");
        }

        if (probe.CrawlabilityScore >= onboardingAutomationOptions.MinCrawlabilityScore)
        {
            supportingReasons.Add($"Crawlability scored {probe.CrawlabilityScore:0.#} against {onboardingAutomationOptions.MinCrawlabilityScore:0.#}.");
        }

        if (probe.CategoryRelevanceScore >= onboardingAutomationOptions.MinCategoryRelevanceScore)
        {
            supportingReasons.Add($"Category relevance scored {probe.CategoryRelevanceScore:0.#} against {onboardingAutomationOptions.MinCategoryRelevanceScore:0.#}.");
        }

        if (probe.CatalogLikelihoodScore >= onboardingAutomationOptions.MinCatalogLikelihoodScore)
        {
            supportingReasons.Add($"Catalog likelihood scored {probe.CatalogLikelihoodScore:0.#} against {onboardingAutomationOptions.MinCatalogLikelihoodScore:0.#}.");
        }

        if (unattendedAutomationRequested && suggestionBreadthPassed)
        {
            supportingReasons.Add($"Automation breadth validated {probe.AutomationReachableCategorySampleCount} category samples, {probe.AutomationReachableProductSampleCount} product samples, and {probe.AutomationRuntimeCompatibleProductSampleCount} runtime-compatible product pages.");
        }

        if (requestedMode == SourceAutomationModes.AutoAcceptAndSeed && autoAcceptBreadthPassed)
        {
            supportingReasons.Add($"Auto-accept breadth validated {probe.AutomationStructuredProductEvidenceSampleCount} product samples with structured product evidence.");
        }

        if (confidenceScore >= onboardingAutomationOptions.SuggestMinConfidenceScore)
        {
            supportingReasons.Add($"Overall confidence scored {confidenceScore:0.#} against the suggestion threshold {onboardingAutomationOptions.SuggestMinConfidenceScore:0.#}.");
        }

        if (requestedMode == SourceAutomationModes.AutoAcceptAndSeed && confidenceScore >= onboardingAutomationOptions.AutoAcceptMinConfidenceScore)
        {
            supportingReasons.Add($"Overall confidence also cleared the auto-accept threshold at {confidenceScore:0.#} against {onboardingAutomationOptions.AutoAcceptMinConfidenceScore:0.#}.");
        }

        var blockingReasons = new List<string>();
        if (string.IsNullOrWhiteSpace(requestedMarket))
        {
            blockingReasons.Add("Automation requires an explicit requested market so source policy stays operator-scoped.");
        }

        if (!marketMatchApproved)
        {
            blockingReasons.Add($"Candidate market metadata ({candidateMarketList}) does not clearly match the requested market '{requestedMarket ?? "unspecified"}'.");
        }

        if (!marketEvidenceStrongEnough)
        {
            blockingReasons.Add($"Candidate market evidence is '{FormatEvidence(searchResult.MarketEvidence)}' with markets [{candidateMarketList}]; automation requires explicit single-market evidence.");
        }

        if (!allowedByGovernance)
        {
            blockingReasons.Add("Governance rejected this candidate.");
        }

        if (!duplicateRiskAccepted)
        {
            blockingReasons.Add($"Duplicate risk scored {duplicateRiskScore:0.#} against the guarded maximum {onboardingAutomationOptions.MaxDuplicateRiskScore:0.#}.");
        }

        if (!representativeValidationPassed)
        {
            blockingReasons.Add($"Representative validation requires both category and product pages to be reachable (category: {FormatPassFail(probe.RepresentativeCategoryPageReachable)}, product: {FormatPassFail(probe.RepresentativeProductPageReachable)}).");
        }

        if (!extractabilityConfidencePassed)
        {
            blockingReasons.Add($"Representative product validation produced runtime-compatible evidence={FormatPassFail(probe.RuntimeExtractionCompatible)} with extractability {probe.ExtractabilityScore:0.#} against {onboardingAutomationOptions.MinExtractabilityScore:0.#}.");
        }

        if (!yieldConfidencePassed)
        {
            blockingReasons.Add($"Predicted downstream yield confidence scored {yieldConfidenceScore:0.#} against the guarded minimum {onboardingAutomationOptions.MinYieldConfidenceScore:0.#}.");
        }

        if (unattendedAutomationRequested && !suggestionBreadthPassed)
        {
            blockingReasons.Add($"Automation sampled {probe.AutomationReachableCategorySampleCount}/{onboardingAutomationOptions.SuggestMinReachableCategorySamples} reachable category pages, {probe.AutomationReachableProductSampleCount}/{onboardingAutomationOptions.SuggestMinReachableProductSamples} reachable product pages, and {probe.AutomationRuntimeCompatibleProductSampleCount}/{onboardingAutomationOptions.SuggestMinRuntimeCompatibleProductSamples} runtime-compatible product pages; that is not enough for unattended suggestion.");
        }

        if (requestedMode == SourceAutomationModes.AutoAcceptAndSeed && !autoAcceptBreadthPassed)
        {
            blockingReasons.Add($"Auto-accept requires broader recurring evidence: {probe.AutomationReachableCategorySampleCount}/{onboardingAutomationOptions.AutoAcceptMinReachableCategorySamples} reachable category pages, {probe.AutomationReachableProductSampleCount}/{onboardingAutomationOptions.AutoAcceptMinReachableProductSamples} reachable product pages, {probe.AutomationRuntimeCompatibleProductSampleCount}/{onboardingAutomationOptions.AutoAcceptMinRuntimeCompatibleProductSamples} runtime-compatible product pages, and {probe.AutomationStructuredProductEvidenceSampleCount}/{onboardingAutomationOptions.AutoAcceptMinStructuredEvidenceProductSamples} structured-evidence product pages.");
        }

        if (!localeAligned)
        {
            blockingReasons.Add($"Candidate locale '{NormalizeOptionalText(searchResult.PreferredLocale) ?? "unspecified"}' does not align cleanly with requested locale '{requestedLocale ?? "unspecified"}'.");
        }

        if (probe.CrawlabilityScore < onboardingAutomationOptions.MinCrawlabilityScore)
        {
            blockingReasons.Add($"Crawlability scored {probe.CrawlabilityScore:0.#} against the guarded minimum {onboardingAutomationOptions.MinCrawlabilityScore:0.#}.");
        }

        if (probe.CategoryRelevanceScore < onboardingAutomationOptions.MinCategoryRelevanceScore)
        {
            blockingReasons.Add($"Category relevance scored {probe.CategoryRelevanceScore:0.#} against the guarded minimum {onboardingAutomationOptions.MinCategoryRelevanceScore:0.#}.");
        }

        if (probe.CatalogLikelihoodScore < onboardingAutomationOptions.MinCatalogLikelihoodScore)
        {
            blockingReasons.Add($"Catalog likelihood scored {probe.CatalogLikelihoodScore:0.#} against the guarded minimum {onboardingAutomationOptions.MinCatalogLikelihoodScore:0.#}.");
        }

        if (confidenceScore < onboardingAutomationOptions.SuggestMinConfidenceScore)
        {
            blockingReasons.Add($"Overall confidence scored {confidenceScore:0.#} against the suggestion threshold {onboardingAutomationOptions.SuggestMinConfidenceScore:0.#}.");
        }

        if (requestedMode == SourceAutomationModes.AutoAcceptAndSeed && confidenceScore < onboardingAutomationOptions.AutoAcceptMinConfidenceScore)
        {
            blockingReasons.Add($"Overall confidence scored {confidenceScore:0.#} against the auto-accept threshold {onboardingAutomationOptions.AutoAcceptMinConfidenceScore:0.#}.");
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
            SuggestionBreadthPassed = suggestionBreadthPassed,
            AutoAcceptBreadthPassed = autoAcceptBreadthPassed,
            LocaleAligned = localeAligned,
            CrawlabilityPassed = probe.CrawlabilityScore >= onboardingAutomationOptions.MinCrawlabilityScore,
            CategoryRelevancePassed = probe.CategoryRelevanceScore >= onboardingAutomationOptions.MinCategoryRelevanceScore,
            CatalogLikelihoodPassed = probe.CatalogLikelihoodScore >= onboardingAutomationOptions.MinCatalogLikelihoodScore,
            SuggestionConfidencePassed = confidenceScore >= onboardingAutomationOptions.SuggestMinConfidenceScore,
            AutoAcceptConfidencePassed = confidenceScore >= onboardingAutomationOptions.AutoAcceptMinConfidenceScore,
            EligibleForSuggestion = eligibleForSuggestion,
            EligibleForAutoAccept = eligibleForAutoAccept,
            EligibleForAutoSeed = eligibleForAutoAccept,
            MarketEvidence = searchResult.MarketEvidence,
            LocaleEvidence = searchResult.LocaleEvidence,
            SupportingReasons = supportingReasons,
            BlockingReasons = blockingReasons.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private static string FormatPassFail(bool value)
    {
        return value ? "yes" : "no";
    }

    private static string FormatValueList(IReadOnlyList<string> values, string fallback)
    {
        return values.Count == 0 ? fallback : string.Join(", ", values);
    }

    private static string FormatEvidence(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "missing" : value.Trim();
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

    private static decimal CalculateDuplicateRiskScore(IReadOnlyCollection<CrawlSource> duplicateSources)
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

        if (probe.AutomationReachableCategorySampleCount >= 2)
        {
            score += 5m;
        }

        if (probe.AutomationReachableProductSampleCount >= 2)
        {
            score += 5m;
        }

        score += Math.Min(10m, probe.AutomationRuntimeCompatibleProductSampleCount * 5m);

        if (probe.AutomationStructuredProductEvidenceSampleCount >= 2)
        {
            score += 5m;
        }

        return Math.Clamp(decimal.Round(score, 2, MidpointRounding.AwayFromZero), 0m, 100m);
    }

    private static bool HasSuggestionAutomationEvidence(SourceCandidateProbeResult probe, SourceOnboardingAutomationOptions options)
    {
        return probe.AutomationReachableCategorySampleCount >= options.SuggestMinReachableCategorySamples
            && probe.AutomationReachableProductSampleCount >= options.SuggestMinReachableProductSamples
            && probe.AutomationRuntimeCompatibleProductSampleCount >= options.SuggestMinRuntimeCompatibleProductSamples;
    }

    private static bool HasAutoAcceptAutomationEvidence(SourceCandidateProbeResult probe, SourceOnboardingAutomationOptions options)
    {
        return probe.AutomationReachableCategorySampleCount >= options.AutoAcceptMinReachableCategorySamples
            && probe.AutomationReachableProductSampleCount >= options.AutoAcceptMinReachableProductSamples
            && probe.AutomationRuntimeCompatibleProductSampleCount >= options.AutoAcceptMinRuntimeCompatibleProductSamples
            && probe.AutomationStructuredProductEvidenceSampleCount >= options.AutoAcceptMinStructuredEvidenceProductSamples;
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
        if (probe.ProbeTimedOut)
        {
            return "Representative page probing timed out before runtime extraction could be confirmed for this candidate.";
        }

        if (probe.ProbeFailed)
        {
            return "Representative page probing failed before runtime extraction could be confirmed for this candidate.";
        }

        if (probe.RuntimeExtractionCompatible)
        {
            return probe.RepresentativeRuntimeProductCount == 1
                ? "Representative runtime extraction succeeded on the sampled product page."
                : $"Representative runtime extraction succeeded on the sampled product page and produced {probe.RepresentativeRuntimeProductCount} products.";
        }

        if (probe.RepresentativeProductPageFetchFailed)
        {
            return "A representative product page was identified, but fetching that page failed before runtime extraction could be confirmed.";
        }

        if (probe.RepresentativeCategoryPageFetchFailed)
        {
            return "A representative category page was identified, but fetching that page failed before runtime extraction could be confirmed.";
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
}