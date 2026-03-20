namespace ProductNormaliser.Core.Normalisation;

public sealed class MeasurementParseResult
{
    public bool Success { get; init; }
    public decimal? NumericValue { get; init; }
    public string? Unit { get; init; }
    public string? Notes { get; init; }
}