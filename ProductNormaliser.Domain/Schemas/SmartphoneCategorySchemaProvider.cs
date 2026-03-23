using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Schemas;

public sealed class SmartphoneCategorySchemaProvider : ICategorySchemaProvider
{
    public const string CategoryKey = "smartphone";

    private static readonly CategorySchema SmartphoneSchema = new()
    {
        CategoryKey = CategoryKey,
        DisplayName = "Smartphones",
        Attributes =
        [
            new CanonicalAttributeDefinition { Key = "brand", DisplayName = "Brand", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.Critical, Description = "Manufacturer brand name." },
            new CanonicalAttributeDefinition { Key = "model_number", DisplayName = "Model Number", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.Critical, Description = "Manufacturer model identifier." },
            new CanonicalAttributeDefinition { Key = "gtin", DisplayName = "GTIN", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Critical, Description = "Global trade item number when published." },
            new CanonicalAttributeDefinition { Key = "screen_size_inch", DisplayName = "Screen Size", ValueType = "decimal", Unit = "inch", IsRequired = true, ConflictSensitivity = ConflictSensitivity.High, Description = "Nominal display size in inches." },
            new CanonicalAttributeDefinition { Key = "native_resolution", DisplayName = "Native Resolution", ValueType = "string", ConflictSensitivity = ConflictSensitivity.High, Description = "Display native resolution." },
            new CanonicalAttributeDefinition { Key = "storage_capacity_gb", DisplayName = "Storage Capacity", ValueType = "integer", Unit = "gb", ConflictSensitivity = ConflictSensitivity.High, Description = "Primary storage capacity in gigabytes." },
            new CanonicalAttributeDefinition { Key = "ram_gb", DisplayName = "RAM", ValueType = "integer", Unit = "gb", ConflictSensitivity = ConflictSensitivity.High, Description = "Installed memory in gigabytes." },
            new CanonicalAttributeDefinition { Key = "operating_system", DisplayName = "Operating System", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Preinstalled operating system." },
            new CanonicalAttributeDefinition { Key = "cellular_generation", DisplayName = "Cellular Generation", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Primary cellular standard such as 4G or 5G." },
            new CanonicalAttributeDefinition { Key = "rear_camera_mp", DisplayName = "Rear Camera", ValueType = "decimal", Unit = "mp", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Main rear camera resolution in megapixels." },
            new CanonicalAttributeDefinition { Key = "battery_capacity_mah", DisplayName = "Battery Capacity", ValueType = "integer", Unit = "mah", ConflictSensitivity = ConflictSensitivity.Low, Description = "Battery capacity in milliamp-hours." },
            new CanonicalAttributeDefinition { Key = "dual_sim", DisplayName = "Dual SIM", ValueType = "boolean", ConflictSensitivity = ConflictSensitivity.Low, Description = "Whether the device supports dual SIM functionality." }
        ]
    };

    public string SupportedCategoryKey => CategoryKey;

    public CategorySchema GetSchema()
    {
        return SmartphoneSchema;
    }
}