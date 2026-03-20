namespace ProductNormaliser.Core.Models;

public sealed class CrawlTarget
{
    public string Url { get; set; } = default!;
    public string CategoryKey { get; set; } = default!;
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}