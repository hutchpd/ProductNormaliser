namespace ProductNormaliser.Core.Models;

public sealed class RawPage
{
    public string Id { get; set; } = default!;
    public string SourceName { get; set; } = default!;
    public string SourceUrl { get; set; } = default!;
    public string CategoryKey { get; set; } = default!;
    public string Html { get; set; } = default!;
    public string ContentHash { get; set; } = default!;
    public int StatusCode { get; set; }
    public string? ContentType { get; set; }
    public DateTime FetchedUtc { get; set; }
}