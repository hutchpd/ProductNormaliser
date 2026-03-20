namespace ProductNormaliser.AdminApi.Contracts;

public sealed class SourceAttributeDisagreementDto
{
    public string SourceName { get; init; } = default!;
    public string CategoryKey { get; init; } = default!;
    public string AttributeKey { get; init; } = default!;
    public int TotalComparisons { get; init; }
    public int TimesDisagreed { get; init; }
    public int TimesWon { get; init; }
    public decimal DisagreementRate { get; init; }
    public decimal WinRate { get; init; }
    public DateTime LastUpdatedUtc { get; init; }
}