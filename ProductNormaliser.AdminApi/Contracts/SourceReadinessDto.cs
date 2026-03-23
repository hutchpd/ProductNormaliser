namespace ProductNormaliser.AdminApi.Contracts;

public sealed class SourceReadinessDto
{
    public string Status { get; init; } = default!;
    public int AssignedCategoryCount { get; init; }
    public int CrawlableCategoryCount { get; init; }
    public string Summary { get; init; } = default!;
}