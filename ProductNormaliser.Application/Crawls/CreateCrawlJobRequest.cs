namespace ProductNormaliser.Application.Crawls;

public sealed class CreateCrawlJobRequest
{
    public string RequestType { get; init; } = string.Empty;
    public IReadOnlyCollection<string> RequestedCategories { get; init; } = [];
    public IReadOnlyCollection<string> RequestedSources { get; init; } = [];
    public IReadOnlyCollection<string> RequestedProductIds { get; init; } = [];
}