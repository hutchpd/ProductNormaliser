namespace ProductNormaliser.AdminApi.Contracts;

public sealed class ProductChangeEventDto
{
    public string CanonicalProductId { get; init; } = default!;
    public string CategoryKey { get; init; } = default!;
    public string AttributeKey { get; init; } = default!;
    public object? OldValue { get; init; }
    public object? NewValue { get; init; }
    public string SourceName { get; init; } = default!;
    public DateTime TimestampUtc { get; init; }
}