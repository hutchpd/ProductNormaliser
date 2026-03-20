namespace ProductNormaliser.Core.Models;

public sealed class ProductFingerprint
{
    public string? Gtin { get; set; }
    public string BrandKey { get; set; } = string.Empty;
    public string ModelKey { get; set; } = string.Empty;
    public string TitleKey { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public IReadOnlyCollection<string> Tokens { get; set; } = [];
}