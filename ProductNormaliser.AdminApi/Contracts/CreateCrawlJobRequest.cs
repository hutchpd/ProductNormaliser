namespace ProductNormaliser.AdminApi.Contracts;

public sealed class CreateCrawlJobRequest
{
    public string RequestType { get; init; } = string.Empty;
    public IReadOnlyList<string> RequestedCategories { get; init; } = [];
    public IReadOnlyList<string> RequestedSources { get; init; } = [];
    public IReadOnlyList<string> RequestedProductIds { get; init; } = [];
}