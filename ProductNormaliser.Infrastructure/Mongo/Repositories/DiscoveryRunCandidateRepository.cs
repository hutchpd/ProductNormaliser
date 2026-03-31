using MongoDB.Driver;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class DiscoveryRunCandidateRepository(MongoDbContext context)
    : MongoRepositoryBase<DiscoveryRunCandidate>(context.DiscoveryRunCandidates), IDiscoveryRunCandidateStore
{
    public async Task<IReadOnlyList<DiscoveryRunCandidate>> ListByRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(candidate => candidate.RunId == runId)
            .SortByDescending(candidate => candidate.ConfidenceScore)
            .ThenBy(candidate => candidate.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DiscoveryRunCandidate>> ListByHostsAsync(IReadOnlyCollection<string> hosts, CancellationToken cancellationToken = default)
    {
        if (hosts.Count == 0)
        {
            return [];
        }

        return await Collection.Find(candidate => hosts.Contains(candidate.Host))
            .SortByDescending(candidate => candidate.UpdatedUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<DiscoveryRunCandidatePage> QueryByRunAsync(string runId, DiscoveryRunCandidateQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var allCandidates = await ListByRunAsync(runId, cancellationToken);
        var summary = BuildSummary(allCandidates);
        var filteredCandidates = ApplyStateFilter(allCandidates, query.StateFilter);
        var orderedCandidates = ApplySort(filteredCandidates, query.Sort).ToArray();
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 12 : query.PageSize, 1, 100);
        var totalCount = orderedCandidates.LongLength;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        if (totalPages > 0 && page > totalPages)
        {
            page = totalPages;
        }

        var items = orderedCandidates
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return new DiscoveryRunCandidatePage
        {
            Items = items,
            StateFilter = string.IsNullOrWhiteSpace(query.StateFilter) ? DiscoveryRunCandidateStateFilters.All : query.StateFilter,
            Sort = string.IsNullOrWhiteSpace(query.Sort) ? DiscoveryRunCandidateSortModes.ReviewPriority : query.Sort,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Summary = summary
        };
    }

    public async Task<DiscoveryRunCandidate?> GetAsync(string runId, string candidateKey, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(candidate => candidate.RunId == runId && candidate.CandidateKey == candidateKey)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertAsync(DiscoveryRunCandidate candidate, CancellationToken cancellationToken = default)
    {
        await Collection.ReplaceOneAsync(
            existing => existing.RunId == candidate.RunId && existing.CandidateKey == candidate.CandidateKey,
            candidate,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<bool> TryUpdateAsync(DiscoveryRunCandidate candidate, int expectedRevision, CancellationToken cancellationToken = default)
    {
        var result = await Collection.ReplaceOneAsync(
            existing => existing.RunId == candidate.RunId
                && existing.CandidateKey == candidate.CandidateKey
                && existing.Revision == expectedRevision,
            candidate,
            new ReplaceOptions { IsUpsert = false },
            cancellationToken);

        return result.ModifiedCount == 1;
    }

    private static DiscoveryRunCandidateRunSummary BuildSummary(IReadOnlyList<DiscoveryRunCandidate> candidates)
    {
        var representativeCategoryFetchFailureCount = candidates.Count(candidate => candidate.Probe.RepresentativeCategoryPageFetchFailed);
        var representativeProductFetchFailureCount = candidates.Count(candidate => candidate.Probe.RepresentativeProductPageFetchFailed);
        var llmMeasuredCandidates = candidates
            .Where(candidate => candidate.Probe.LlmElapsedMs is > 0)
            .ToArray();
        var llmBudgetedCandidates = candidates
            .Where(candidate => candidate.Probe.LlmBudgetMs is > 0)
            .ToArray();
        var averageLlmBudgetMs = llmBudgetedCandidates.Length == 0
            ? null
            : (long?)Math.Round(llmBudgetedCandidates.Average(candidate => candidate.Probe.LlmBudgetMs ?? 0L), MidpointRounding.AwayFromZero);
        var averageLlmBudgetUtilizationPercent = llmMeasuredCandidates.Length == 0
            ? (decimal?)null
            : decimal.Round(
                (decimal)llmMeasuredCandidates.Average(candidate =>
                    (candidate.Probe.LlmElapsedMs ?? 0L) / (double)Math.Max(1L, candidate.Probe.LlmBudgetMs ?? 0L)) * 100m,
                1,
                MidpointRounding.AwayFromZero);
        var autoAcceptBlockers = BuildAutoAcceptBlockers(candidates);

        return new DiscoveryRunCandidateRunSummary
        {
            RunCandidateCount = candidates.Count,
            ActiveCandidateCount = candidates.Count(candidate => IsInActiveQueue(candidate.State)),
            ArchivedCandidateCount = candidates.Count(candidate => IsInArchivedQueue(candidate.State)),
            LlmMeasuredCandidateCount = llmMeasuredCandidates.Length,
            LlmBudgetProbeCappedCandidateCount = candidates.Count(candidate => candidate.Probe.LlmBudgetLimitedByProbe),
            ProbeTimeoutCandidateCount = candidates.Count(candidate => candidate.Probe.ProbeTimedOut),
            RepresentativePageFetchFailureCandidateCount = candidates.Count(candidate => candidate.Probe.RepresentativeCategoryPageFetchFailed || candidate.Probe.RepresentativeProductPageFetchFailed),
            RepresentativeCategoryFetchFailureCount = representativeCategoryFetchFailureCount,
            RepresentativeProductFetchFailureCount = representativeProductFetchFailureCount,
            LlmTimeoutCandidateCount = candidates.Count(candidate => candidate.Probe.LlmTimedOut),
            AverageLlmBudgetMs = averageLlmBudgetMs,
            AverageLlmBudgetUtilizationPercent = averageLlmBudgetUtilizationPercent,
            AutoAcceptBlockers = autoAcceptBlockers
        };
    }

    private static IReadOnlyList<DiscoveryRunCandidateBlockerSummary> BuildAutoAcceptBlockers(IReadOnlyList<DiscoveryRunCandidate> candidates)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            foreach (var blockerCode in GetAutoAcceptBlockerCodes(candidate))
            {
                counts[blockerCode] = counts.TryGetValue(blockerCode, out var count) ? count + 1 : 1;
            }
        }

        return counts
            .Select(entry => new DiscoveryRunCandidateBlockerSummary
            {
                Code = entry.Key,
                Label = GetAutoAcceptBlockerLabel(entry.Key),
                Count = entry.Value
            })
            .OrderByDescending(entry => entry.Count)
            .ThenBy(entry => entry.Label, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
    }

    private static IEnumerable<string> GetAutoAcceptBlockerCodes(DiscoveryRunCandidate candidate)
    {
        if (string.Equals(candidate.State, DiscoveryRunCandidateStates.AutoAccepted, StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        if (string.Equals(candidate.AutomationAssessment.RequestedMode, SourceAutomationModes.OperatorAssisted, StringComparison.OrdinalIgnoreCase))
        {
            yield return "mode_operator_assisted";
        }

        if (string.Equals(candidate.AutomationAssessment.RequestedMode, SourceAutomationModes.SuggestAccept, StringComparison.OrdinalIgnoreCase))
        {
            yield return "mode_suggest_only";
        }

        if (candidate.AlreadyRegistered)
        {
            yield return "already_registered";
        }

        if (string.Equals(candidate.AutomationAssessment.RequestedMode, SourceAutomationModes.AutoAcceptAndSeed, StringComparison.OrdinalIgnoreCase)
            && candidate.AutomationAssessment.EligibleForAutoAccept
            && !candidate.AlreadyRegistered)
        {
            yield return "auto_accept_cap_consumed";
        }

        if (!candidate.AutomationAssessment.MarketMatchApproved)
        {
            yield return "market_mismatch";
        }

        if (!candidate.AutomationAssessment.MarketEvidenceStrongEnough)
        {
            yield return "market_evidence_weak";
        }

        if (!candidate.AutomationAssessment.GovernancePassed)
        {
            yield return "governance_rejected";
        }

        if (!candidate.AutomationAssessment.DuplicateRiskAccepted)
        {
            yield return "duplicate_risk_high";
        }

        if (!candidate.AutomationAssessment.RepresentativeValidationPassed)
        {
            yield return "representative_validation_failed";
        }

        if (!candidate.AutomationAssessment.ExtractabilityConfidencePassed)
        {
            yield return "runtime_extractability_failed";
        }

        if (!candidate.AutomationAssessment.YieldConfidencePassed)
        {
            yield return "yield_confidence_low";
        }

        if (!candidate.AutomationAssessment.SuggestionBreadthPassed)
        {
            yield return "suggestion_breadth_thin";
        }

        if (string.Equals(candidate.AutomationAssessment.RequestedMode, SourceAutomationModes.AutoAcceptAndSeed, StringComparison.OrdinalIgnoreCase)
            && !candidate.AutomationAssessment.AutoAcceptBreadthPassed)
        {
            yield return "auto_accept_breadth_thin";
        }

        if (!candidate.AutomationAssessment.LocaleAligned)
        {
            yield return "locale_misaligned";
        }

        if (!candidate.AutomationAssessment.CrawlabilityPassed)
        {
            yield return "crawlability_low";
        }

        if (!candidate.AutomationAssessment.CategoryRelevancePassed)
        {
            yield return "category_relevance_low";
        }

        if (!candidate.AutomationAssessment.CatalogLikelihoodPassed)
        {
            yield return "catalog_likelihood_low";
        }

        if (!candidate.AutomationAssessment.SuggestionConfidencePassed)
        {
            yield return "suggestion_confidence_low";
        }

        if (string.Equals(candidate.AutomationAssessment.RequestedMode, SourceAutomationModes.AutoAcceptAndSeed, StringComparison.OrdinalIgnoreCase)
            && !candidate.AutomationAssessment.AutoAcceptConfidencePassed)
        {
            yield return "auto_accept_confidence_low";
        }
    }

    private static string GetAutoAcceptBlockerLabel(string code)
    {
        return code switch
        {
            "mode_operator_assisted" => "Run mode was operator-assisted only",
            "mode_suggest_only" => "Run mode stopped at suggestion rather than auto-accept",
            "already_registered" => "Host was already registered",
            "auto_accept_cap_consumed" => "Run had already consumed its auto-accept allowance",
            "market_mismatch" => "Candidate market did not match the requested market",
            "market_evidence_weak" => "Market evidence was not explicit enough for auto-accept",
            "governance_rejected" => "Governance policy rejected the candidate",
            "duplicate_risk_high" => "Duplicate risk was too high",
            "representative_validation_failed" => "Representative page validation did not fully succeed",
            "runtime_extractability_failed" => "Runtime extractability evidence stayed below the guarded floor",
            "yield_confidence_low" => "Predicted downstream yield confidence stayed below policy",
            "suggestion_breadth_thin" => "Suggestion breadth evidence was too thin",
            "auto_accept_breadth_thin" => "Recurring evidence for auto-accept was too thin",
            "locale_misaligned" => "Locale did not align cleanly with the run scope",
            "crawlability_low" => "Crawlability stayed below the guarded floor",
            "category_relevance_low" => "Category relevance stayed below the guarded floor",
            "catalog_likelihood_low" => "Catalog likelihood stayed below the guarded floor",
            "suggestion_confidence_low" => "Overall confidence stayed below the suggestion threshold",
            "auto_accept_confidence_low" => "Overall confidence stayed below the auto-accept threshold",
            _ => code
        };
    }

    private static IEnumerable<DiscoveryRunCandidate> ApplyStateFilter(IEnumerable<DiscoveryRunCandidate> candidates, string? stateFilter)
    {
        return (stateFilter ?? DiscoveryRunCandidateStateFilters.All) switch
        {
            DiscoveryRunCandidateStateFilters.Active => candidates.Where(candidate => IsInActiveQueue(candidate.State)),
            DiscoveryRunCandidateStateFilters.Archived => candidates.Where(candidate => IsInArchivedQueue(candidate.State)),
            DiscoveryRunCandidateStateFilters.All => candidates,
            var specificState => candidates.Where(candidate => string.Equals(candidate.State, specificState, StringComparison.OrdinalIgnoreCase))
        };
    }

    private static IOrderedEnumerable<DiscoveryRunCandidate> ApplySort(IEnumerable<DiscoveryRunCandidate> candidates, string? sort)
    {
        return (sort ?? DiscoveryRunCandidateSortModes.ReviewPriority) switch
        {
            DiscoveryRunCandidateSortModes.ConfidenceDesc => candidates
                .OrderByDescending(candidate => candidate.ConfidenceScore)
                .ThenBy(candidate => candidate.DuplicateRiskScore)
                .ThenByDescending(GetAutomationDecisionRank)
                .ThenBy(GetBlockingReasonCount)
                .ThenBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase),
            DiscoveryRunCandidateSortModes.DuplicateRiskAsc => candidates
                .OrderBy(candidate => candidate.DuplicateRiskScore)
                .ThenByDescending(candidate => candidate.ConfidenceScore)
                .ThenByDescending(GetAutomationDecisionRank)
                .ThenBy(GetBlockingReasonCount)
                .ThenBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase),
            DiscoveryRunCandidateSortModes.UpdatedDesc => candidates
                .OrderByDescending(GetRecencyTimestamp)
                .ThenByDescending(candidate => candidate.ConfidenceScore)
                .ThenBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase),
            _ => candidates
                .OrderBy(GetReviewPriorityRank)
                .ThenByDescending(candidate => candidate.ConfidenceScore)
                .ThenBy(candidate => candidate.DuplicateRiskScore)
                .ThenByDescending(GetAutomationDecisionRank)
                .ThenBy(GetBlockingReasonCount)
                .ThenBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static bool IsInActiveQueue(string state)
    {
        return !string.Equals(state, DiscoveryRunCandidateStates.Dismissed, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(state, DiscoveryRunCandidateStates.Archived, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(state, DiscoveryRunCandidateStates.Superseded, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInArchivedQueue(string state)
    {
        return string.Equals(state, DiscoveryRunCandidateStates.Dismissed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(state, DiscoveryRunCandidateStates.Archived, StringComparison.OrdinalIgnoreCase)
            || string.Equals(state, DiscoveryRunCandidateStates.Superseded, StringComparison.OrdinalIgnoreCase);
    }

    private static int GetReviewPriorityRank(DiscoveryRunCandidate candidate)
    {
        return candidate.State switch
        {
            DiscoveryRunCandidateStates.Suggested => 0,
            DiscoveryRunCandidateStates.Failed => 1,
            DiscoveryRunCandidateStates.ManuallyAccepted => 2,
            DiscoveryRunCandidateStates.AutoAccepted => 2,
            DiscoveryRunCandidateStates.Pending => 3,
            DiscoveryRunCandidateStates.Probing => 3,
            DiscoveryRunCandidateStates.AwaitingLlm => 3,
            DiscoveryRunCandidateStates.Dismissed => 4,
            DiscoveryRunCandidateStates.Archived => 4,
            DiscoveryRunCandidateStates.Superseded => 4,
            _ => 5
        };
    }

    private static int GetAutomationDecisionRank(DiscoveryRunCandidate candidate)
    {
        return candidate.AutomationAssessment.Decision switch
        {
            SourceCandidateAutomationAssessment.DecisionAutoAcceptAndSeed => 2,
            SourceCandidateAutomationAssessment.DecisionSuggestAccept => 1,
            _ => 0
        };
    }

    private static int GetBlockingReasonCount(DiscoveryRunCandidate candidate)
    {
        return candidate.AutomationAssessment.BlockingReasons?.Count ?? 0;
    }

    private static DateTime GetRecencyTimestamp(DiscoveryRunCandidate candidate)
    {
        return candidate.ArchivedUtc
            ?? candidate.DecisionUtc
            ?? candidate.UpdatedUtc;
    }
}