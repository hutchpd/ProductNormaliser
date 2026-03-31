using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

public sealed class SourceAutomationPosture
{
    public const string StatusAdvisory = "advisory";
    public const string StatusHealthy = "healthy";
    public const string StatusDowngraded = "downgraded";
    public const string StatusManualReview = "manual_review";
    public const string StatusQuarantined = "quarantined";

    public const string ActionNone = "none";
    public const string ActionKeepCurrentMode = "keep_current_mode";
    public const string ActionDowngradeToSuggest = "downgrade_to_suggest";
    public const string ActionFlagManualReview = "flag_manual_review";
    public const string ActionPauseReseeding = "pause_reseeding";

    public string Status { get; init; } = StatusAdvisory;
    public string EffectiveMode { get; init; } = SourceAutomationModes.OperatorAssisted;
    public string RecommendedAction { get; init; } = ActionNone;
    public int SnapshotCount { get; init; }
    public decimal DiscoveryBreadthScore { get; init; }
    public decimal ProductTargetPromotionRate { get; init; }
    public decimal DownstreamYieldScore { get; init; }
    public decimal TrustTrendDelta { get; init; }
    public decimal ExtractabilityTrendDelta { get; init; }
    public int RecurringDiscoveryRunCount { get; init; }
    public int RecurringDiscoveryAcceptedCount { get; init; }
    public decimal RecurringDiscoveryAcceptanceRate { get; init; }
    public IReadOnlyList<string> SupportingReasons { get; init; } = [];
    public IReadOnlyList<string> BlockingReasons { get; init; } = [];
}