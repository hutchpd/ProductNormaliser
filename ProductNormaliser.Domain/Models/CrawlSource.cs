namespace ProductNormaliser.Core.Models;

public sealed class CrawlSource
{
    public string Id { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string BaseUrl { get; set; } = default!;
    public string Host { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public List<string> SupportedCategoryKeys { get; set; } = [];
    public SourceDiscoveryProfile DiscoveryProfile { get; set; } = new();
    public SourceThrottlingPolicy ThrottlingPolicy { get; set; } = new();
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}