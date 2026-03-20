namespace ProductNormaliser.Core.Models;

public sealed class ProductChangeEvent
{
    public string Id { get; set; } = default!;
    public string CanonicalProductId { get; set; } = default!;
    public string CategoryKey { get; set; } = default!;
    public string AttributeKey { get; set; } = default!;
    public object? OldValue { get; set; }
    public object? NewValue { get; set; }
    public string SourceName { get; set; } = default!;
    public DateTime TimestampUtc { get; set; }
}