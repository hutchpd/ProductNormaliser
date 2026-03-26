namespace ProductNormaliser.AdminApi.Contracts;

public sealed class SourceAutomationPostureDto
{
    public string Status { get; init; } = "advisory";
    public string EffectiveMode { get; init; } = "operator_assisted";
    public string RecommendedAction { get; init; } = "none";
    public int SnapshotCount { get; init; }
    public decimal DiscoveryBreadthScore { get; init; }
    public decimal ProductTargetPromotionRate { get; init; }
    public decimal DownstreamYieldScore { get; init; }
    public decimal TrustTrendDelta { get; init; }
    public decimal ExtractabilityTrendDelta { get; init; }
    public IReadOnlyList<string> SupportingReasons { get; init; } = [];
    public IReadOnlyList<string> BlockingReasons { get; init; } = [];
}