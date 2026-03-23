namespace ProductNormaliser.Core.Models;

public sealed class CanonicalAttributeDefinition
{
    public string Key { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string ValueType { get; set; } = default!;
    public string? Unit { get; set; }
    public bool IsRequired { get; set; }
    public ConflictSensitivity ConflictSensitivity { get; set; } = ConflictSensitivity.Medium;
    public string Description { get; set; } = default!;
}