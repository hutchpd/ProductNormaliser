namespace ProductNormaliser.Core.Models;

public sealed class CanonicalProduct
{
    public string Id { get; set; } = default!;
    public string CategoryKey { get; set; } = default!;
    public string Brand { get; set; } = default!;
    public string? ModelNumber { get; set; }
    public string? Gtin { get; set; }
    public string DisplayName { get; set; } = default!;

    public Dictionary<string, CanonicalAttributeValue> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ProductSourceLink> Sources { get; set; } = [];
    public List<string> OfferIds { get; set; } = [];

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}