using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

public sealed class SourceAutomationPostureEvaluator(SourceAutomationMonitoringOptions options)
{
    private readonly SourceAutomationMonitoringOptions options = options ?? new SourceAutomationMonitoringOptions();

    public SourceAutomationPosture Evaluate(string configuredMode, IReadOnlyList<SourceQualitySnapshot> snapshots, SourceRecurringDiscoveryHistory? recurringDiscoveryHistory = null)
    {
        var normalizedMode = SourceAutomationModes.Normalize(configuredMode);
        var recurringHistory = recurringDiscoveryHistory ?? new SourceRecurringDiscoveryHistory();
        var orderedSnapshots = snapshots
            .OrderByDescending(snapshot => snapshot.TimestampUtc)
            .ToArray();
        var latestSnapshotsByCategory = orderedSnapshots
            .GroupBy(snapshot => snapshot.CategoryKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        var current = Aggregate(latestSnapshotsByCategory);
        var trustTrendDelta = CalculateAverageDelta(orderedSnapshots, snapshot => snapshot.HistoricalTrustScore);
        var extractabilityTrendDelta = CalculateAverageDelta(orderedSnapshots, snapshot => snapshot.ExtractabilityRate);
        var snapshotSupportsSuggestion = latestSnapshotsByCategory.Length > 0
            && current.TrustScore >= options.SuggestMinTrustScore
            && current.DiscoveryBreadthScore >= options.SuggestMinDiscoveryBreadthScore
            && current.ProductTargetPromotionRate >= options.SuggestMinProductPromotionRate
            && current.ExtractabilityRate >= options.SuggestMinExtractabilityRate
            && current.NoProductRate <= options.SuggestMaxNoProductRate
            && current.DownstreamYieldScore >= options.SuggestMinDownstreamYieldScore
            && current.SpecStabilityScore >= options.SuggestMinSpecStabilityScore
            && current.PriceVolatilityScore <= options.SuggestMaxPriceVolatilityScore;
        var snapshotSupportsAutoAccept = snapshotSupportsSuggestion
            && current.TrustScore >= options.AutoAcceptMinTrustScore
            && current.DiscoveryBreadthScore >= options.AutoAcceptMinDiscoveryBreadthScore
            && current.ProductTargetPromotionRate >= options.AutoAcceptMinProductPromotionRate
            && current.ExtractabilityRate >= options.AutoAcceptMinExtractabilityRate
            && current.NoProductRate <= options.AutoAcceptMaxNoProductRate
            && current.DownstreamYieldScore >= options.AutoAcceptMinDownstreamYieldScore
            && current.SpecStabilityScore >= options.AutoAcceptMinSpecStabilityScore
            && current.PriceVolatilityScore <= options.AutoAcceptMaxPriceVolatilityScore
            && trustTrendDelta >= -options.MaxTrustScoreDropForAutoAccept
            && extractabilityTrendDelta >= -options.MaxExtractabilityDropForAutoAccept;
        var recurringSupportsSuggestion = recurringHistory.DiscoveryRunCount >= options.RecurringDiscoverySuggestMinRuns
            && recurringHistory.AcceptanceRate >= options.RecurringDiscoverySuggestMinAcceptanceRate;
        var recurringSupportsAutoAccept = recurringHistory.DiscoveryRunCount >= options.RecurringDiscoveryAutoAcceptMinRuns
            && recurringHistory.AcceptanceRate >= options.RecurringDiscoveryAutoAcceptMinAcceptanceRate;
        var shouldQuarantine = latestSnapshotsByCategory.Length > 0
            && (current.TrustScore < options.QuarantineMinTrustScore
                || current.DiscoveryBreadthScore < options.QuarantineMinDiscoveryBreadthScore
                || current.ExtractabilityRate < options.QuarantineMinExtractabilityRate
                || current.NoProductRate > options.QuarantineMaxNoProductRate);
        var shouldFlagRecurringManualReview = recurringHistory.DiscoveryRunCount >= options.RecurringDiscoverySuggestMinRuns
            && recurringHistory.DiscoveryRunCount > 0
            && decimal.Round((decimal)(recurringHistory.DismissedCandidateCount + recurringHistory.SupersededCandidateCount) / recurringHistory.DiscoveryRunCount, 4, MidpointRounding.AwayFromZero) >= options.RecurringDiscoveryManualReviewDismissalRate;

        var supportsSuggestion = snapshotSupportsSuggestion
            || (!shouldQuarantine && recurringSupportsSuggestion);
        var supportsAutoAccept = snapshotSupportsAutoAccept
            || (!shouldQuarantine && (snapshotSupportsSuggestion || latestSnapshotsByCategory.Length == 0) && recurringSupportsAutoAccept);

        var supportingReasons = BuildSupportingReasons(current, supportsSuggestion, supportsAutoAccept, recurringHistory, recurringSupportsSuggestion, recurringSupportsAutoAccept);
        var blockingReasons = BuildBlockingReasons(current, trustTrendDelta, extractabilityTrendDelta, latestSnapshotsByCategory.Length, recurringHistory, shouldFlagRecurringManualReview);

        if (normalizedMode == SourceAutomationModes.OperatorAssisted)
        {
            return new SourceAutomationPosture
            {
                Status = SourceAutomationPosture.StatusAdvisory,
                EffectiveMode = SourceAutomationModes.OperatorAssisted,
                RecommendedAction = SourceAutomationPosture.ActionNone,
                SnapshotCount = latestSnapshotsByCategory.Length,
                DiscoveryBreadthScore = current.DiscoveryBreadthScore,
                ProductTargetPromotionRate = current.ProductTargetPromotionRate,
                DownstreamYieldScore = current.DownstreamYieldScore,
                TrustTrendDelta = trustTrendDelta,
                ExtractabilityTrendDelta = extractabilityTrendDelta,
                RecurringDiscoveryRunCount = recurringHistory.DiscoveryRunCount,
                RecurringDiscoveryAcceptedCount = recurringHistory.AcceptedCandidateCount,
                RecurringDiscoveryAcceptanceRate = recurringHistory.AcceptanceRate,
                SupportingReasons = latestSnapshotsByCategory.Length == 0
                    ? supportingReasons
                    : ["Longitudinal automation review is available, but this source remains operator-assisted by policy."],
                BlockingReasons = latestSnapshotsByCategory.Length == 0
                    ? blockingReasons
                    : shouldFlagRecurringManualReview
                        ? blockingReasons
                        : []
            };
        }

        var effectiveMode = shouldQuarantine || shouldFlagRecurringManualReview
            ? SourceAutomationModes.OperatorAssisted
            : normalizedMode == SourceAutomationModes.AutoAcceptAndSeed
                ? supportsAutoAccept
                    ? SourceAutomationModes.AutoAcceptAndSeed
                    : supportsSuggestion
                        ? SourceAutomationModes.SuggestAccept
                        : SourceAutomationModes.OperatorAssisted
                : supportsSuggestion
                    ? SourceAutomationModes.SuggestAccept
                    : SourceAutomationModes.OperatorAssisted;

        var status = shouldQuarantine
            ? SourceAutomationPosture.StatusQuarantined
            : shouldFlagRecurringManualReview
                ? SourceAutomationPosture.StatusManualReview
            : effectiveMode == normalizedMode
                ? SourceAutomationPosture.StatusHealthy
                : effectiveMode == SourceAutomationModes.SuggestAccept
                    ? SourceAutomationPosture.StatusDowngraded
                    : SourceAutomationPosture.StatusManualReview;
        var recommendedAction = shouldQuarantine
            ? SourceAutomationPosture.ActionPauseReseeding
            : shouldFlagRecurringManualReview
                ? SourceAutomationPosture.ActionFlagManualReview
            : effectiveMode == normalizedMode
                ? SourceAutomationPosture.ActionKeepCurrentMode
                : effectiveMode == SourceAutomationModes.SuggestAccept
                    ? SourceAutomationPosture.ActionDowngradeToSuggest
                    : SourceAutomationPosture.ActionFlagManualReview;

        return new SourceAutomationPosture
        {
            Status = status,
            EffectiveMode = effectiveMode,
            RecommendedAction = recommendedAction,
            SnapshotCount = latestSnapshotsByCategory.Length,
            DiscoveryBreadthScore = current.DiscoveryBreadthScore,
            ProductTargetPromotionRate = current.ProductTargetPromotionRate,
            DownstreamYieldScore = current.DownstreamYieldScore,
            TrustTrendDelta = trustTrendDelta,
            ExtractabilityTrendDelta = extractabilityTrendDelta,
            RecurringDiscoveryRunCount = recurringHistory.DiscoveryRunCount,
            RecurringDiscoveryAcceptedCount = recurringHistory.AcceptedCandidateCount,
            RecurringDiscoveryAcceptanceRate = recurringHistory.AcceptanceRate,
            SupportingReasons = supportingReasons,
            BlockingReasons = blockingReasons
        };
    }

    private IReadOnlyList<string> BuildSupportingReasons(
        AggregatedSnapshot current,
        bool supportsSuggestion,
        bool supportsAutoAccept,
        SourceRecurringDiscoveryHistory recurringHistory,
        bool recurringSupportsSuggestion,
        bool recurringSupportsAutoAccept)
    {
        var reasons = new List<string>();

        if (current.DiscoveryBreadthScore >= options.SuggestMinDiscoveryBreadthScore)
        {
            reasons.Add($"Discovery breadth stayed at {ToPercent(current.DiscoveryBreadthScore):0.#}% across recent source history.");
        }

        if (current.ProductTargetPromotionRate >= options.SuggestMinProductPromotionRate)
        {
            reasons.Add($"Promoted product-target coverage held at {ToPercent(current.ProductTargetPromotionRate):0.#}%.");
        }

        if (current.ExtractabilityRate >= options.SuggestMinExtractabilityRate)
        {
            reasons.Add($"Runtime-compatible extraction held at {ToPercent(current.ExtractabilityRate):0.#}%.");
        }

        if (current.NoProductRate <= options.SuggestMaxNoProductRate)
        {
            reasons.Add($"No-product rate remained bounded at {ToPercent(current.NoProductRate):0.#}%.");
        }

        if (supportsAutoAccept)
        {
            reasons.Add("Longitudinal evidence still supports guarded auto-accept.");
        }
        else if (supportsSuggestion)
        {
            reasons.Add("Longitudinal evidence still supports unattended suggestion.");
        }

        if (recurringSupportsAutoAccept)
        {
            reasons.Add($"Recurring discovery accepted {recurringHistory.AcceptedCandidateCount} candidates across {recurringHistory.DiscoveryRunCount} rediscovery runs.");
        }
        else if (recurringSupportsSuggestion)
        {
            reasons.Add($"Recurring discovery kept resurfacing this source with a {ToPercent(recurringHistory.AcceptanceRate):0.#}% acceptance rate.");
        }

        return reasons;
    }

    private IReadOnlyList<string> BuildBlockingReasons(
        AggregatedSnapshot current,
        decimal trustTrendDelta,
        decimal extractabilityTrendDelta,
        int snapshotCount,
        SourceRecurringDiscoveryHistory recurringHistory,
        bool shouldFlagRecurringManualReview)
    {
        var reasons = new List<string>();

        if (snapshotCount == 0)
        {
            reasons.Add("No longitudinal source snapshots are recorded yet.");
        }

        if (current.DiscoveryBreadthScore < options.SuggestMinDiscoveryBreadthScore)
        {
            reasons.Add($"Discovery breadth fell to {ToPercent(current.DiscoveryBreadthScore):0.#}%.");
        }

        if (current.ProductTargetPromotionRate < options.SuggestMinProductPromotionRate)
        {
            reasons.Add($"Reachable product-target promotion fell to {ToPercent(current.ProductTargetPromotionRate):0.#}%.");
        }

        if (current.ExtractabilityRate < options.SuggestMinExtractabilityRate)
        {
            reasons.Add($"Runtime-compatible extraction fell to {ToPercent(current.ExtractabilityRate):0.#}%.");
        }

        if (current.NoProductRate > options.SuggestMaxNoProductRate)
        {
            reasons.Add($"No-product rate rose to {ToPercent(current.NoProductRate):0.#}%.");
        }

        if (current.DownstreamYieldScore < options.SuggestMinDownstreamYieldScore)
        {
            reasons.Add($"Downstream product yield fell to {ToPercent(current.DownstreamYieldScore):0.#}%.");
        }

        if (current.SpecStabilityScore < options.SuggestMinSpecStabilityScore)
        {
            reasons.Add($"Specification stability fell to {ToPercent(current.SpecStabilityScore):0.#}%.");
        }

        if (current.PriceVolatilityScore > options.SuggestMaxPriceVolatilityScore)
        {
            reasons.Add($"Catalog drift increased: price volatility reached {ToPercent(current.PriceVolatilityScore):0.#}%.");
        }

        if (trustTrendDelta < 0m)
        {
            reasons.Add($"Trust trend moved {ToPercent(trustTrendDelta):0.#} points over the monitoring window.");
        }

        if (extractabilityTrendDelta < 0m)
        {
            reasons.Add($"Extractability trend moved {ToPercent(extractabilityTrendDelta):0.#} points over the monitoring window.");
        }

        if (shouldFlagRecurringManualReview)
        {
            reasons.Add($"Recurring discovery ended in dismiss or supersede decisions for {recurringHistory.DismissedCandidateCount + recurringHistory.SupersededCandidateCount} of {recurringHistory.DiscoveryRunCount} rediscovery runs.");
        }

        return reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private decimal CalculateAverageDelta(IReadOnlyList<SourceQualitySnapshot> snapshots, Func<SourceQualitySnapshot, decimal> selector)
    {
        var deltas = snapshots
            .GroupBy(snapshot => snapshot.CategoryKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(snapshot => snapshot.TimestampUtc)
                .Take(Math.Max(2, options.ReviewWindowSnapshots))
                .ToArray())
            .Where(group => group.Length >= 2)
            .Select(group => selector(group[0]) - selector(group[^1]))
            .ToArray();

        if (deltas.Length == 0)
        {
            return 0m;
        }

        return decimal.Round(deltas.Average(), 4, MidpointRounding.AwayFromZero);
    }

    private static AggregatedSnapshot Aggregate(IReadOnlyList<SourceQualitySnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return new AggregatedSnapshot();
        }

        return new AggregatedSnapshot(
            TrustScore: decimal.Round(snapshots.Average(snapshot => snapshot.HistoricalTrustScore), 4, MidpointRounding.AwayFromZero),
            DiscoveryBreadthScore: decimal.Round(snapshots.Average(snapshot => snapshot.DiscoveryBreadthScore), 4, MidpointRounding.AwayFromZero),
            ProductTargetPromotionRate: decimal.Round(snapshots.Average(snapshot => snapshot.ProductTargetPromotionRate), 4, MidpointRounding.AwayFromZero),
            ExtractabilityRate: decimal.Round(snapshots.Average(snapshot => snapshot.ExtractabilityRate), 4, MidpointRounding.AwayFromZero),
            NoProductRate: decimal.Round(snapshots.Average(snapshot => snapshot.NoProductRate), 4, MidpointRounding.AwayFromZero),
            DownstreamYieldScore: decimal.Round(snapshots.Average(snapshot => snapshot.DownstreamYieldScore), 4, MidpointRounding.AwayFromZero),
            SpecStabilityScore: decimal.Round(snapshots.Average(snapshot => snapshot.SpecStabilityScore), 4, MidpointRounding.AwayFromZero),
            PriceVolatilityScore: decimal.Round(snapshots.Average(snapshot => snapshot.PriceVolatilityScore), 4, MidpointRounding.AwayFromZero));
    }

    private static decimal ToPercent(decimal value)
    {
        return decimal.Round(value * 100m, 2, MidpointRounding.AwayFromZero);
    }

    private sealed record AggregatedSnapshot(
        decimal TrustScore = 0m,
        decimal DiscoveryBreadthScore = 0m,
        decimal ProductTargetPromotionRate = 0m,
        decimal ExtractabilityRate = 0m,
        decimal NoProductRate = 0m,
        decimal DownstreamYieldScore = 0m,
        decimal SpecStabilityScore = 0m,
        decimal PriceVolatilityScore = 0m);
}