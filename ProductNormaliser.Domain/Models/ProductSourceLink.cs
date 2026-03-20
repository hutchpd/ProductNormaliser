namespace ProductNormaliser.Core.Models;

public sealed class ProductSourceLink
{
    public string SourceName { get; set; } = default!;
    public string SourceProductId { get; set; } = default!;
    public string SourceUrl { get; set; } = default!;
    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
}