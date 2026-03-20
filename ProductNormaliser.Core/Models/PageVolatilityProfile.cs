namespace ProductNormaliser.Core.Models;

public sealed class PageVolatilityProfile
{
    public decimal PageVolatilityScore { get; set; }
    public decimal ChangeFrequencyScore { get; set; }
    public decimal PriceVolatilityScore { get; set; }
    public decimal SpecStabilityScore { get; set; }
    public decimal FailureRate { get; set; }
}