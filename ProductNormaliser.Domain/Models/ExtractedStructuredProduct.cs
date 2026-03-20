namespace ProductNormaliser.Core.Models;

public sealed class ExtractedStructuredProduct
{
    public string SourceUrl { get; set; } = default!;
    public string Url
    {
        get => SourceUrl;
        set => SourceUrl = value;
    }

    public string? Name { get; set; }
    public string? Title
    {
        get => Name;
        set => Name = value;
    }

    public string? Brand { get; set; }
    public string? ModelNumber { get; set; }
    public string? Gtin { get; set; }
    public string RawJson { get; set; } = default!;

    public Dictionary<string, string> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ExtractedOffer> Offers { get; set; } = [];
}