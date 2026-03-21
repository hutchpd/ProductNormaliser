namespace ProductNormaliser.Application.Crawls;

public sealed class CrawlJobTargetDescriptor
{
    public string SourceName { get; init; } = string.Empty;
    public string SourceUrl { get; init; } = string.Empty;
    public string CategoryKey { get; init; } = string.Empty;
}