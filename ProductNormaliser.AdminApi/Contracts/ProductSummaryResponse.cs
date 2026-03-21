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
    public DateTime UpdatedUtc { get; init; }
}