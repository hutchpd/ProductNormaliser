namespace ProductNormaliser.Core.Models;

public sealed class SourceAttributeDisagreement
{
    public string Id { get; set; } = default!;
    public string SourceName { get; set; } = default!;
    public string CategoryKey { get; set; } = default!;
    public string AttributeKey { get; set; } = default!;
    public int TotalComparisons { get; set; }
    public int TimesDisagreed { get; set; }
    public int TimesWon { get; set; }
    public decimal DisagreementRate { get; set; }
    public decimal WinRate { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
}