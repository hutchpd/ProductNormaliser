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
            new CanonicalAttributeDefinition { Key = "model_family", DisplayName = "Model Family", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.High, Description = "Commercial model family such as iPhone 15 or Galaxy S24." },
            new CanonicalAttributeDefinition { Key = "variant_name", DisplayName = "Variant Name", ValueType = "string", ConflictSensitivity = ConflictSensitivity.High, Description = "Sellable variant label such as Pro, Plus, or Ultra." },
            new CanonicalAttributeDefinition { Key = "manufacturer_part_number", DisplayName = "Manufacturer Part Number", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Critical, Description = "Manufacturer SKU or part number for the specific product variant." },
            new CanonicalAttributeDefinition { Key = "gtin", DisplayName = "GTIN", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Critical, Description = "Global trade item number when published." },
            new CanonicalAttributeDefinition { Key = "regional_variant", DisplayName = "Regional Variant", ValueType = "string", ConflictSensitivity = ConflictSensitivity.High, Description = "Market or regional variant such as UK, EU, or US." },
            new CanonicalAttributeDefinition { Key = "release_year", DisplayName = "Release Year", ValueType = "integer", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Commercial release year for the device family or variant." },
            new CanonicalAttributeDefinition { Key = "colour", DisplayName = "Colour", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Manufacturer colour or finish name for the sellable variant." },
            new CanonicalAttributeDefinition { Key = "storage_capacity_gb", DisplayName = "Storage Capacity", ValueType = "integer", Unit = "gb", IsRequired = true, ConflictSensitivity = ConflictSensitivity.High, Description = "Primary internal storage capacity in gigabytes." },
            new CanonicalAttributeDefinition { Key = "ram_gb", DisplayName = "RAM", ValueType = "integer", Unit = "gb", ConflictSensitivity = ConflictSensitivity.High, Description = "Installed memory in gigabytes." },
            new CanonicalAttributeDefinition { Key = "operating_system", DisplayName = "Operating System", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.High, Description = "Preinstalled operating system family or edition." },
            new CanonicalAttributeDefinition { Key = "cellular_generation", DisplayName = "Cellular Generation", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.High, Description = "Primary cellular standard such as 4G or 5G." },
            new CanonicalAttributeDefinition { Key = "sim_form_factor", DisplayName = "SIM Form Factor", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Primary SIM form factor such as Nano-SIM or eSIM." },
            new CanonicalAttributeDefinition { Key = "esim_support", DisplayName = "eSIM Support", ValueType = "boolean", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Whether the device supports eSIM activation." },
            new CanonicalAttributeDefinition { Key = "dual_sim", DisplayName = "Dual SIM", ValueType = "boolean", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Whether the device supports dual SIM functionality." },
            new CanonicalAttributeDefinition { Key = "carrier_lock_status", DisplayName = "Carrier Lock Status", ValueType = "string", ConflictSensitivity = ConflictSensitivity.High, Description = "Whether the device is unlocked or tied to a specific carrier." },
            new CanonicalAttributeDefinition { Key = "screen_size_inch", DisplayName = "Screen Size", ValueType = "decimal", Unit = "inch", IsRequired = true, ConflictSensitivity = ConflictSensitivity.High, Description = "Nominal display size in inches." },
            new CanonicalAttributeDefinition { Key = "native_resolution", DisplayName = "Native Resolution", ValueType = "string", ConflictSensitivity = ConflictSensitivity.High, Description = "Display native resolution." },
            new CanonicalAttributeDefinition { Key = "display_technology", DisplayName = "Display Technology", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Display technology such as OLED, AMOLED, or LCD." },
            new CanonicalAttributeDefinition { Key = "refresh_rate_hz", DisplayName = "Refresh Rate", ValueType = "integer", Unit = "hz", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Advertised display refresh rate in hertz." },
            new CanonicalAttributeDefinition { Key = "chipset_model", DisplayName = "Chipset Model", ValueType = "string", ConflictSensitivity = ConflictSensitivity.High, Description = "Primary application processor or system-on-chip model." },
            new CanonicalAttributeDefinition { Key = "rear_camera_primary_mp", DisplayName = "Rear Camera Primary", ValueType = "decimal", Unit = "mp", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Primary rear camera resolution in megapixels." },
            new CanonicalAttributeDefinition { Key = "front_camera_mp", DisplayName = "Front Camera", ValueType = "decimal", Unit = "mp", ConflictSensitivity = ConflictSensitivity.Low, Description = "Front-facing camera resolution in megapixels." },
            new CanonicalAttributeDefinition { Key = "battery_capacity_mah", DisplayName = "Battery Capacity", ValueType = "integer", Unit = "mah", ConflictSensitivity = ConflictSensitivity.Low, Description = "Battery capacity in milliamp-hours." },
            new CanonicalAttributeDefinition { Key = "charging_port", DisplayName = "Charging Port", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Primary wired charging and data connector." },
            new CanonicalAttributeDefinition { Key = "wireless_charging", DisplayName = "Wireless Charging", ValueType = "boolean", ConflictSensitivity = ConflictSensitivity.Low, Description = "Whether the device supports wireless charging." },
            new CanonicalAttributeDefinition { Key = "nfc", DisplayName = "NFC", ValueType = "boolean", ConflictSensitivity = ConflictSensitivity.Low, Description = "Whether the device supports near-field communication." },
            new CanonicalAttributeDefinition { Key = "ip_rating", DisplayName = "IP Rating", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Advertised ingress protection rating such as IP67 or IP68." },
            new CanonicalAttributeDefinition { Key = "width_mm", DisplayName = "Width", ValueType = "decimal", Unit = "mm", ConflictSensitivity = ConflictSensitivity.Low, Description = "Product width in millimetres." },
            new CanonicalAttributeDefinition { Key = "height_mm", DisplayName = "Height", ValueType = "decimal", Unit = "mm", ConflictSensitivity = ConflictSensitivity.Low, Description = "Product height in millimetres." },
            new CanonicalAttributeDefinition { Key = "depth_mm", DisplayName = "Depth", ValueType = "decimal", Unit = "mm", ConflictSensitivity = ConflictSensitivity.Low, Description = "Product depth in millimetres." },
            new CanonicalAttributeDefinition { Key = "weight_g", DisplayName = "Weight", ValueType = "integer", Unit = "g", ConflictSensitivity = ConflictSensitivity.Low, Description = "Product weight in grams." }
        ]
    };

    public string SupportedCategoryKey => CategoryKey;

    public CategorySchema GetSchema()
    {
        return SmartphoneSchema;
    }
}