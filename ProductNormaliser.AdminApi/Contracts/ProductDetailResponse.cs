namespace ProductNormaliser.AdminApi.Contracts;

public sealed class ProductDetailResponse
{
    public string Id { get; init; } = default!;
    public string CategoryKey { get; init; } = default!;
    public string Brand { get; init; } = default!;
    public string? ModelNumber { get; init; }
    public string? Gtin { get; init; }
    public string DisplayName { get; init; } = default!;
    public DateTime CreatedUtc { get; init; }
    public DateTime UpdatedUtc { get; init; }
    public IReadOnlyCollection<ProductAttributeDetailDto> Attributes { get; init; } = [];
    public IReadOnlyCollection<SourceProductDetailDto> SourceProducts { get; init; } = [];
}