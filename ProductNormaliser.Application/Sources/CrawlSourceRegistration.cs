using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

public sealed class CrawlSourceRegistration
{
    public string SourceId { get; init; } = default!;
    public string DisplayName { get; init; } = default!;
    public string BaseUrl { get; init; } = default!;
    public string? Description { get; init; }
    public bool IsEnabled { get; init; } = true;
    public IReadOnlyCollection<string> AllowedMarkets { get; init; } = [];
    public string? PreferredLocale { get; init; }
    public IReadOnlyCollection<string> SupportedCategoryKeys { get; init; } = [];
    public SourceDiscoveryProfile? DiscoveryProfile { get; init; }
    public SourceThrottlingPolicy? ThrottlingPolicy { get; init; }
}