using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Schemas;

public sealed class TabletCategorySchemaProvider : ICategorySchemaProvider
{
    public const string CategoryKey = "tablet";

    private static readonly CategorySchema TabletSchema = new()
    {
        CategoryKey = CategoryKey,
        DisplayName = "Tablets",
        Attributes =
        [
            new CanonicalAttributeDefinition { Key = "brand", DisplayName = "Brand", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.Critical, Description = "Manufacturer brand name." },
            new CanonicalAttributeDefinition { Key = "model_number", DisplayName = "Model Number", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.Critical, Description = "Manufacturer model identifier." },
            new CanonicalAttributeDefinition { Key = "gtin", DisplayName = "GTIN", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Critical, Description = "Global trade item number when published." },
            new CanonicalAttributeDefinition { Key = "display_size_inch", DisplayName = "Display Size", ValueType = "decimal", Unit = "inch", IsRequired = true, ConflictSensitivity = ConflictSensitivity.High, Description = "Nominal display size in inches." },
            new CanonicalAttributeDefinition { Key = "native_resolution", DisplayName = "Native Resolution", ValueType = "string", ConflictSensitivity = ConflictSensitivity.High, Description = "Display native resolution." },
            new CanonicalAttributeDefinition { Key = "storage_capacity_gb", DisplayName = "Storage Capacity", ValueType = "integer", Unit = "gb", ConflictSensitivity = ConflictSensitivity.High, Description = "Primary storage capacity in gigabytes." },
            new CanonicalAttributeDefinition { Key = "ram_gb", DisplayName = "RAM", ValueType = "integer", Unit = "gb", ConflictSensitivity = ConflictSensitivity.High, Description = "Installed memory in gigabytes." },
            new CanonicalAttributeDefinition { Key = "operating_system", DisplayName = "Operating System", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Preinstalled operating system." },
            new CanonicalAttributeDefinition { Key = "connectivity", DisplayName = "Connectivity", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Primary connectivity posture such as Wi-Fi or Wi-Fi + Cellular." },
            new CanonicalAttributeDefinition { Key = "battery_life_hours", DisplayName = "Battery Life", ValueType = "decimal", Unit = "hour", ConflictSensitivity = ConflictSensitivity.Low, Description = "Advertised battery life in hours." },
            new CanonicalAttributeDefinition { Key = "weight_g", DisplayName = "Weight", ValueType = "integer", Unit = "g", ConflictSensitivity = ConflictSensitivity.Low, Description = "Product weight in grams." },
            new CanonicalAttributeDefinition { Key = "stylus_support", DisplayName = "Stylus Support", ValueType = "boolean", ConflictSensitivity = ConflictSensitivity.Low, Description = "Whether the tablet supports an active stylus." }
        ]
    };

    public string SupportedCategoryKey => CategoryKey;

    public CategorySchema GetSchema()
    {
        return TabletSchema;
    }
}