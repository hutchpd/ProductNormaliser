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

        return new DiscoveryRunCandidateRunSummary
        {
            RunCandidateCount = candidates.Count,
            ActiveCandidateCount = candidates.Count(candidate => IsInActiveQueue(candidate.State)),
            ArchivedCandidateCount = candidates.Count(candidate => IsInArchivedQueue(candidate.State)),
            ProbeTimeoutCandidateCount = candidates.Count(candidate => candidate.Probe.ProbeTimedOut),
            RepresentativePageFetchFailureCandidateCount = candidates.Count(candidate => candidate.Probe.RepresentativeCategoryPageFetchFailed || candidate.Probe.RepresentativeProductPageFetchFailed),
            RepresentativeCategoryFetchFailureCount = representativeCategoryFetchFailureCount,
            RepresentativeProductFetchFailureCount = representativeProductFetchFailureCount,
            LlmTimeoutCandidateCount = candidates.Count(candidate => candidate.Probe.LlmTimedOut)
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