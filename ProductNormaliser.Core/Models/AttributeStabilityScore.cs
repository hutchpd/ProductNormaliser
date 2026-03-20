namespace ProductNormaliser.Core.Models;

public sealed class AttributeStabilityScore
{
    public string CategoryKey { get; set; } = default!;
    public string AttributeKey { get; set; } = default!;
    public int ChangeCount { get; set; }
    public int OscillationCount { get; set; }
    public int DistinctValueCount { get; set; }
    public decimal StabilityScore { get; set; }
    public bool IsSuspicious { get; set; }
    public string? SuspicionReason { get; set; }
}