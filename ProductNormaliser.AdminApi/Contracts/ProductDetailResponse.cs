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
    public int SourceCount { get; init; }
    public int EvidenceCount { get; init; }
    public int ConflictAttributeCount { get; init; }
    public bool HasConflict { get; init; }
    public decimal CompletenessScore { get; init; }
    public string CompletenessStatus { get; init; } = default!;
    public int PopulatedKeyAttributeCount { get; init; }
    public int ExpectedKeyAttributeCount { get; init; }
    public string FreshnessStatus { get; init; } = default!;
    public int FreshnessAgeDays { get; init; }
    public IReadOnlyCollection<ProductKeyAttributeDto> KeyAttributes { get; init; } = [];
    public IReadOnlyCollection<ProductAttributeDetailDto> Attributes { get; init; } = [];
    public IReadOnlyCollection<SourceProductDetailDto> SourceProducts { get; init; } = [];
}