namespace ProductNormaliser.Core.Models;

public sealed class ExtractedStructuredProduct
{
    public string Url { get; set; } = default!;
    public string? Brand { get; set; }
    public string? ModelNumber { get; set; }
    public string? Gtin { get; set; }
    public string? Title { get; set; }
    public Dictionary<string, SourceAttributeValue> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}