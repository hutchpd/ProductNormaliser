namespace ProductNormaliser.Core.Models;

public sealed class CategorySchema
{
    public string CategoryKey { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public List<CanonicalAttributeDefinition> Attributes { get; set; } = [];
}