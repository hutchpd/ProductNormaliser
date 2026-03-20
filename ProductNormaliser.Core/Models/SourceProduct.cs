namespace ProductNormaliser.Core.Models;

public sealed class SourceProduct
{
    public string Id { get; set; } = default!;
    public string SourceName { get; set; } = default!;
    public string SourceUrl { get; set; } = default!;
    public string CategoryKey { get; set; } = default!;

    public string? Brand { get; set; }
    public string? ModelNumber { get; set; }
    public string? Gtin { get; set; }
    public string? Title { get; set; }

    public Dictionary<string, SourceAttributeValue> RawAttributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, NormalisedAttributeValue> NormalisedAttributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ProductOffer> Offers { get; set; } = [];

    public string RawSchemaJson { get; set; } = default!;
    public DateTime FetchedUtc { get; set; }
}