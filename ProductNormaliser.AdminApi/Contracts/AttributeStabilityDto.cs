namespace ProductNormaliser.AdminApi.Contracts;

public sealed class AttributeStabilityDto
{
    public string CategoryKey { get; init; } = default!;
    public string AttributeKey { get; init; } = default!;
    public int ChangeCount { get; init; }
    public int OscillationCount { get; init; }
    public int DistinctValueCount { get; init; }
    public decimal StabilityScore { get; init; }
    public bool IsSuspicious { get; init; }
    public string? SuspicionReason { get; init; }
}