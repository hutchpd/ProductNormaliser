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
            new CanonicalAttributeDefinition { Key = "model_family", DisplayName = "Model Family", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.High, Description = "Commercial model family such as iPad Air or Galaxy Tab S." },
            new CanonicalAttributeDefinition { Key = "variant_name", DisplayName = "Variant Name", ValueType = "string", ConflictSensitivity = ConflictSensitivity.High, Description = "Sellable variant label such as Air, Pro, or FE." },
            new CanonicalAttributeDefinition { Key = "manufacturer_part_number", DisplayName = "Manufacturer Part Number", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Critical, Description = "Manufacturer SKU or part number for the specific tablet variant." },
            new CanonicalAttributeDefinition { Key = "gtin", DisplayName = "GTIN", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Critical, Description = "Global trade item number when published." },
            new CanonicalAttributeDefinition { Key = "regional_variant", DisplayName = "Regional Variant", ValueType = "string", ConflictSensitivity = ConflictSensitivity.High, Description = "Market or regional variant such as UK, EU, or US." },
            new CanonicalAttributeDefinition { Key = "release_year", DisplayName = "Release Year", ValueType = "integer", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Commercial release year for the device family or variant." },
            new CanonicalAttributeDefinition { Key = "colour", DisplayName = "Colour", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Manufacturer colour or finish name for the sellable variant." },
            new CanonicalAttributeDefinition { Key = "display_size_inch", DisplayName = "Display Size", ValueType = "decimal", Unit = "inch", IsRequired = true, ConflictSensitivity = ConflictSensitivity.High, Description = "Nominal display size in inches." },
            new CanonicalAttributeDefinition { Key = "native_resolution", DisplayName = "Native Resolution", ValueType = "string", ConflictSensitivity = ConflictSensitivity.High, Description = "Display native resolution." },
            new CanonicalAttributeDefinition { Key = "display_technology", DisplayName = "Display Technology", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Display technology such as LCD, OLED, or Mini LED." },
            new CanonicalAttributeDefinition { Key = "refresh_rate_hz", DisplayName = "Refresh Rate", ValueType = "integer", Unit = "hz", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Advertised display refresh rate in hertz." },
            new CanonicalAttributeDefinition { Key = "storage_capacity_gb", DisplayName = "Storage Capacity", ValueType = "integer", Unit = "gb", IsRequired = true, ConflictSensitivity = ConflictSensitivity.High, Description = "Primary storage capacity in gigabytes." },
            new CanonicalAttributeDefinition { Key = "ram_gb", DisplayName = "RAM", ValueType = "integer", Unit = "gb", ConflictSensitivity = ConflictSensitivity.High, Description = "Installed memory in gigabytes." },
            new CanonicalAttributeDefinition { Key = "operating_system", DisplayName = "Operating System", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.High, Description = "Preinstalled operating system." },
            new CanonicalAttributeDefinition { Key = "connectivity", DisplayName = "Connectivity", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.High, Description = "Primary connectivity posture such as Wi-Fi or Wi-Fi + Cellular." },
            new CanonicalAttributeDefinition { Key = "cellular_generation", DisplayName = "Cellular Generation", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Primary cellular standard for connected models such as 4G or 5G." },
            new CanonicalAttributeDefinition { Key = "chipset_model", DisplayName = "Chipset Model", ValueType = "string", ConflictSensitivity = ConflictSensitivity.High, Description = "Primary application processor or system-on-chip model." },
            new CanonicalAttributeDefinition { Key = "battery_life_hours", DisplayName = "Battery Life", ValueType = "decimal", Unit = "hour", ConflictSensitivity = ConflictSensitivity.Low, Description = "Advertised battery life in hours." },
            new CanonicalAttributeDefinition { Key = "battery_capacity_mah", DisplayName = "Battery Capacity", ValueType = "integer", Unit = "mah", ConflictSensitivity = ConflictSensitivity.Low, Description = "Battery capacity in milliamp-hours when published." },
            new CanonicalAttributeDefinition { Key = "charging_port", DisplayName = "Charging Port", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Primary wired charging and data connector." },
            new CanonicalAttributeDefinition { Key = "weight_g", DisplayName = "Weight", ValueType = "integer", Unit = "g", ConflictSensitivity = ConflictSensitivity.Low, Description = "Product weight in grams." },
            new CanonicalAttributeDefinition { Key = "width_mm", DisplayName = "Width", ValueType = "decimal", Unit = "mm", ConflictSensitivity = ConflictSensitivity.Low, Description = "Product width in millimetres." },
            new CanonicalAttributeDefinition { Key = "height_mm", DisplayName = "Height", ValueType = "decimal", Unit = "mm", ConflictSensitivity = ConflictSensitivity.Low, Description = "Product height in millimetres." },
            new CanonicalAttributeDefinition { Key = "depth_mm", DisplayName = "Depth", ValueType = "decimal", Unit = "mm", ConflictSensitivity = ConflictSensitivity.Low, Description = "Product depth in millimetres." },
            new CanonicalAttributeDefinition { Key = "stylus_support", DisplayName = "Stylus Support", ValueType = "boolean", ConflictSensitivity = ConflictSensitivity.Low, Description = "Whether the tablet supports an active stylus." },
            new CanonicalAttributeDefinition { Key = "keyboard_support", DisplayName = "Keyboard Support", ValueType = "boolean", ConflictSensitivity = ConflictSensitivity.Low, Description = "Whether the tablet supports a first-party or official keyboard accessory." },
            new CanonicalAttributeDefinition { Key = "rear_camera_primary_mp", DisplayName = "Rear Camera Primary", ValueType = "decimal", Unit = "mp", ConflictSensitivity = ConflictSensitivity.Low, Description = "Primary rear camera resolution in megapixels." },
            new CanonicalAttributeDefinition { Key = "front_camera_mp", DisplayName = "Front Camera", ValueType = "decimal", Unit = "mp", ConflictSensitivity = ConflictSensitivity.Low, Description = "Front-facing camera resolution in megapixels." }
        ]
    };

    public string SupportedCategoryKey => CategoryKey;

    public CategorySchema GetSchema()
    {
        return TabletSchema;
    }
}