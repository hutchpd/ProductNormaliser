namespace ProductNormaliser.AdminApi.Contracts;

public sealed class SourceAttributeValueDto
{
    public string AttributeKey { get; init; } = default!;
    public string? Value { get; init; }
    public string ValueType { get; init; } = default!;
    public string? Unit { get; init; }
    public string? SourcePath { get; init; }
}