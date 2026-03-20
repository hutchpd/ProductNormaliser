namespace ProductNormaliser.Core.Models;

public sealed class ExtractedOffer
{
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public string? Availability { get; set; }
    public string RawJson { get; set; } = default!;
}