namespace ProductNormaliser.Infrastructure.Crawling;

public sealed class SemanticChangeDetail
{
    public string AttributeKey { get; init; } = default!;
    public object? OldValue { get; init; }
    public object? NewValue { get; init; }
    public string ChangeType { get; init; } = default!;
}