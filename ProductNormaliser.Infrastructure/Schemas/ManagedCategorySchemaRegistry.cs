using ProductNormaliser.Application.Categories;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Infrastructure.Schemas;

public sealed class ManagedCategorySchemaRegistry(CategorySchemaRegistry baseRegistry, ICategoryMetadataStore categoryMetadataStore) : ICategorySchemaRegistry
{
    public ICategorySchemaProvider? GetProvider(string categoryKey)
    {
        return baseRegistry.GetProvider(categoryKey);
    }

    public CategorySchema? GetSchema(string categoryKey)
    {
        var schema = baseRegistry.GetSchema(categoryKey);
        if (schema is null)
        {
            return null;
        }

        var managedAttributes = categoryMetadataStore.Get(categoryKey)?.ManagedSchemaAttributes;
        if (managedAttributes is null || managedAttributes.Count == 0)
        {
            return CloneSchema(schema);
        }

        return new CategorySchema
        {
            CategoryKey = schema.CategoryKey,
            DisplayName = schema.DisplayName,
            Attributes = managedAttributes.Select(CloneAttribute).ToList()
        };
    }

    public bool TryGetSchema(string categoryKey, out CategorySchema schema)
    {
        schema = default!;

        if (GetSchema(categoryKey) is not { } resolvedSchema)
        {
            return false;
        }

        schema = resolvedSchema;
        return true;
    }

    private static CategorySchema CloneSchema(CategorySchema schema)
    {
        return new CategorySchema
        {
            CategoryKey = schema.CategoryKey,
            DisplayName = schema.DisplayName,
            Attributes = schema.Attributes.Select(CloneAttribute).ToList()
        };
    }

    private static CanonicalAttributeDefinition CloneAttribute(CanonicalAttributeDefinition attribute)
    {
        return new CanonicalAttributeDefinition
        {
            Key = attribute.Key,
            DisplayName = attribute.DisplayName,
            ValueType = attribute.ValueType,
            Unit = attribute.Unit,
            IsRequired = attribute.IsRequired,
            ConflictSensitivity = attribute.ConflictSensitivity,
            Description = attribute.Description
        };
    }
}