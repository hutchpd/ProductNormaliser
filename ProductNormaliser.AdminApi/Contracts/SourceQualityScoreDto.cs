namespace ProductNormaliser.AdminApi.Contracts;

public sealed class SourceQualityScoreDto
{
    public string SourceName { get; init; } = default!;
    public int SourceProductCount { get; init; }
    public decimal AverageMappedAttributes { get; init; }
    public decimal CoveragePercent { get; init; }
    public decimal AverageAttributeConfidence { get; init; }
    public decimal AgreementPercent { get; init; }
    public decimal QualityScore { get; init; }
}