namespace ProductNormaliser.Application.Sources;

public sealed class SourceAutomationMonitoringOptions
{
    public const string SectionName = "SourceAutomationMonitoring";

    public int ReviewWindowSnapshots { get; set; } = 6;
    public decimal SuggestMinTrustScore { get; set; } = 0.65m;
    public decimal SuggestMinDiscoveryBreadthScore { get; set; } = 0.55m;
    public decimal SuggestMinProductPromotionRate { get; set; } = 0.40m;
    public decimal SuggestMinExtractabilityRate { get; set; } = 0.55m;
    public decimal SuggestMaxNoProductRate { get; set; } = 0.45m;
    public decimal SuggestMinDownstreamYieldScore { get; set; } = 0.45m;
    public decimal SuggestMinSpecStabilityScore { get; set; } = 0.45m;
    public decimal SuggestMaxPriceVolatilityScore { get; set; } = 0.85m;
    public decimal AutoAcceptMinTrustScore { get; set; } = 0.80m;
    public decimal AutoAcceptMinDiscoveryBreadthScore { get; set; } = 0.70m;
    public decimal AutoAcceptMinProductPromotionRate { get; set; } = 0.55m;
    public decimal AutoAcceptMinExtractabilityRate { get; set; } = 0.70m;
    public decimal AutoAcceptMaxNoProductRate { get; set; } = 0.30m;
    public decimal AutoAcceptMinDownstreamYieldScore { get; set; } = 0.60m;
    public decimal AutoAcceptMinSpecStabilityScore { get; set; } = 0.60m;
    public decimal AutoAcceptMaxPriceVolatilityScore { get; set; } = 0.65m;
    public decimal MaxTrustScoreDropForAutoAccept { get; set; } = 0.10m;
    public decimal MaxExtractabilityDropForAutoAccept { get; set; } = 0.15m;
    public decimal QuarantineMinTrustScore { get; set; } = 0.40m;
    public decimal QuarantineMinDiscoveryBreadthScore { get; set; } = 0.20m;
    public decimal QuarantineMinExtractabilityRate { get; set; } = 0.20m;
    public decimal QuarantineMaxNoProductRate { get; set; } = 0.80m;
    public int RecurringDiscoverySuggestMinRuns { get; set; } = 2;
    public decimal RecurringDiscoverySuggestMinAcceptanceRate { get; set; } = 0.50m;
    public int RecurringDiscoveryAutoAcceptMinRuns { get; set; } = 3;
    public decimal RecurringDiscoveryAutoAcceptMinAcceptanceRate { get; set; } = 0.75m;
    public decimal RecurringDiscoveryManualReviewDismissalRate { get; set; } = 0.75m;
}