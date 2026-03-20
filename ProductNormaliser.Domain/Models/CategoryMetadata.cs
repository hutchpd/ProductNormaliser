namespace ProductNormaliser.Core.Models;

public sealed class CategoryMetadata
{
    public string CategoryKey { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string FamilyKey { get; set; } = default!;
    public string FamilyDisplayName { get; set; } = default!;
    public string IconKey { get; set; } = default!;
    public CrawlSupportStatus CrawlSupportStatus { get; set; }
    public decimal SchemaCompletenessScore { get; set; }
    public bool IsEnabled { get; set; }
}