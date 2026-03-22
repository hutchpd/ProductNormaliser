namespace ProductNormaliser.AdminApi.Contracts;

public sealed class ProductSummaryResponse
{
    public string Id { get; init; } = default!;
    public string CategoryKey { get; init; } = default!;
    public string Brand { get; init; } = default!;
    public string? ModelNumber { get; init; }
    public string? Gtin { get; init; }
    public string DisplayName { get; init; } = default!;
    public int SourceCount { get; init; }
    public int AttributeCount { get; init; }
    public int EvidenceCount { get; init; }
    public int ConflictAttributeCount { get; init; }
    public bool HasConflict { get; init; }
    public decimal CompletenessScore { get; init; }
    public string CompletenessStatus { get; init; } = default!;
    public int PopulatedKeyAttributeCount { get; init; }
    public int ExpectedKeyAttributeCount { get; init; }
    public string FreshnessStatus { get; init; } = default!;
    public int FreshnessAgeDays { get; init; }
    public IReadOnlyList<ProductKeyAttributeDto> KeyAttributes { get; init; } = [];
    public DateTime UpdatedUtc { get; init; }
}