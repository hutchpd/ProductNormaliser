namespace ProductNormaliser.AdminApi.Contracts;

public sealed class SourceLastActivityDto
{
    public DateTime TimestampUtc { get; init; }
    public string Status { get; init; } = default!;
    public long DurationMs { get; init; }
    public int ExtractedProductCount { get; init; }
    public bool HadMeaningfulChange { get; init; }
    public string? MeaningfulChangeSummary { get; init; }
    public string? ErrorMessage { get; init; }
}