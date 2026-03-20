namespace ProductNormaliser.AdminApi.Contracts;

public sealed class AttributeCoverageDetailDto
{
    public string AttributeKey { get; init; } = default!;
    public string DisplayName { get; init; } = default!;
    public int PresentProductCount { get; init; }
    public int MissingProductCount { get; init; }
    public decimal CoveragePercent { get; init; }
    public int ConflictProductCount { get; init; }
    public decimal ConflictPercent { get; init; }
    public decimal AverageConfidence { get; init; }
    public decimal AgreementPercent { get; init; }
    public decimal ReliabilityScore { get; init; }
}