using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

public sealed class CrawlSourceUpdate
{
    public string DisplayName { get; init; } = default!;
    public string BaseUrl { get; init; } = default!;
    public string? Description { get; init; }
    public IReadOnlyCollection<string>? AllowedMarkets { get; init; }
    public string? PreferredLocale { get; init; }
    public SourceDiscoveryProfile? DiscoveryProfile { get; init; }
}