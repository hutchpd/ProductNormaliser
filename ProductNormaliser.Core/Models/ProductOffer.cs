namespace ProductNormaliser.Core.Models;

public sealed class ProductOffer
{
    public string Id { get; set; } = default!;
    public string SourceName { get; set; } = default!;
    public string SourceUrl { get; set; } = default!;
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public string? Availability { get; set; }
    public DateTime ObservedUtc { get; set; }
}