namespace ProductNormaliser.AdminApi.Contracts;

public sealed class SourceProductDetailDto
{
    public string Id { get; init; } = default!;
    public string SourceName { get; init; } = default!;
    public string SourceUrl { get; init; } = default!;
    public string? Brand { get; init; }
    public string? ModelNumber { get; init; }
    public string? Gtin { get; init; }
    public string? Title { get; init; }
    public string RawSchemaJson { get; init; } = default!;
    public IReadOnlyCollection<SourceAttributeValueDto> RawAttributes { get; init; } = [];
}