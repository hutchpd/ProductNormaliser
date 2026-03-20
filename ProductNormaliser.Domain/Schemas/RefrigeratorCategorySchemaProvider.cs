using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Schemas;

public sealed class RefrigeratorCategorySchemaProvider : ICategorySchemaProvider
{
    public const string CategoryKey = "refrigerator";

    private static readonly CategorySchema RefrigeratorSchema = new()
    {
        CategoryKey = CategoryKey,
        DisplayName = "Refrigerators",
        Attributes =
        [
            new CanonicalAttributeDefinition { Key = "brand", DisplayName = "Brand", ValueType = "string", IsRequired = true, Description = "Manufacturer brand name." },
            new CanonicalAttributeDefinition { Key = "model_number", DisplayName = "Model Number", ValueType = "string", IsRequired = true, Description = "Manufacturer model identifier." },
            new CanonicalAttributeDefinition { Key = "total_capacity_litre", DisplayName = "Total Capacity", ValueType = "integer", Unit = "litre", Description = "Combined storage capacity in litres." },
            new CanonicalAttributeDefinition { Key = "fridge_capacity_litre", DisplayName = "Fridge Capacity", ValueType = "integer", Unit = "litre", Description = "Fresh food compartment capacity in litres." },
            new CanonicalAttributeDefinition { Key = "freezer_capacity_litre", DisplayName = "Freezer Capacity", ValueType = "integer", Unit = "litre", Description = "Freezer compartment capacity in litres." },
            new CanonicalAttributeDefinition { Key = "installation_type", DisplayName = "Installation Type", ValueType = "string", Description = "Freestanding, integrated, or built-in installation type." },
            new CanonicalAttributeDefinition { Key = "energy_rating", DisplayName = "Energy Rating", ValueType = "string", Description = "Published energy rating class." },
            new CanonicalAttributeDefinition { Key = "frost_free", DisplayName = "Frost Free", ValueType = "boolean", Description = "Whether the refrigerator is frost free." },
            new CanonicalAttributeDefinition { Key = "width_mm", DisplayName = "Width", ValueType = "decimal", Unit = "mm", Description = "Product width in millimetres." },
            new CanonicalAttributeDefinition { Key = "height_mm", DisplayName = "Height", ValueType = "decimal", Unit = "mm", Description = "Product height in millimetres." },
            new CanonicalAttributeDefinition { Key = "depth_mm", DisplayName = "Depth", ValueType = "decimal", Unit = "mm", Description = "Product depth in millimetres." }
        ]
    };

    public string SupportedCategoryKey => CategoryKey;

    public CategorySchema GetSchema()
    {
        return RefrigeratorSchema;
    }
}