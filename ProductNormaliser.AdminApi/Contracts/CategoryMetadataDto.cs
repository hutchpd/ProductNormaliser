namespace ProductNormaliser.AdminApi.Contracts;

public sealed class CategoryMetadataDto
{
    public string CategoryKey { get; init; } = default!;
    public string DisplayName { get; init; } = default!;
    public string FamilyKey { get; init; } = default!;
    public string FamilyDisplayName { get; init; } = default!;
    public string IconKey { get; init; } = default!;
    public string CrawlSupportStatus { get; init; } = default!;
    public decimal SchemaCompletenessScore { get; init; }
    public bool IsEnabled { get; init; }
}