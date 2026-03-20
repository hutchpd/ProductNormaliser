namespace ProductNormaliser.Application.Sources;

public sealed class CrawlSourceUpdate
{
    public string DisplayName { get; init; } = default!;
    public string BaseUrl { get; init; } = default!;
    public string? Description { get; init; }
}