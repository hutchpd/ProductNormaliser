using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Schemas;

public sealed class HeadphonesCategorySchemaProvider : ICategorySchemaProvider
{
    public const string CategoryKey = "headphones";

    private static readonly CategorySchema HeadphonesSchema = new()
    {
        CategoryKey = CategoryKey,
        DisplayName = "Headphones",
        Attributes =
        [
            new CanonicalAttributeDefinition { Key = "brand", DisplayName = "Brand", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.Critical, Description = "Manufacturer brand name." },
            new CanonicalAttributeDefinition { Key = "model_number", DisplayName = "Model Number", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.Critical, Description = "Manufacturer model identifier." },
            new CanonicalAttributeDefinition { Key = "model_family", DisplayName = "Model Family", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.High, Description = "Commercial model family such as QuietComfort or WH-1000X." },
            new CanonicalAttributeDefinition { Key = "variant_name", DisplayName = "Variant Name", ValueType = "string", ConflictSensitivity = ConflictSensitivity.High, Description = "Sellable variant label such as Pro, Max, or Lite." },
            new CanonicalAttributeDefinition { Key = "manufacturer_part_number", DisplayName = "Manufacturer Part Number", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Critical, Description = "Manufacturer SKU or part number for the specific headphone variant." },
            new CanonicalAttributeDefinition { Key = "gtin", DisplayName = "GTIN", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Critical, Description = "Global trade item number when published." },
            new CanonicalAttributeDefinition { Key = "release_year", DisplayName = "Release Year", ValueType = "integer", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Commercial release year for the product family or variant." },
            new CanonicalAttributeDefinition { Key = "colour", DisplayName = "Colour", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Manufacturer colour or finish name for the sellable variant." },
            new CanonicalAttributeDefinition { Key = "form_factor", DisplayName = "Form Factor", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.High, Description = "Headphone form factor such as over-ear or in-ear." },
            new CanonicalAttributeDefinition { Key = "connection_type", DisplayName = "Connection Type", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.High, Description = "Primary audio connection such as wired or Bluetooth." },
            new CanonicalAttributeDefinition { Key = "wireless", DisplayName = "Wireless", ValueType = "boolean", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Whether the headphones operate wirelessly." },
            new CanonicalAttributeDefinition { Key = "bluetooth_version", DisplayName = "Bluetooth Version", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Low, Description = "Advertised Bluetooth version for wireless models." },
            new CanonicalAttributeDefinition { Key = "multipoint_support", DisplayName = "Multipoint Support", ValueType = "boolean", ConflictSensitivity = ConflictSensitivity.Low, Description = "Whether the headphones can maintain multiple simultaneous device pairings." },
            new CanonicalAttributeDefinition { Key = "noise_cancelling", DisplayName = "Noise Cancelling", ValueType = "boolean", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Whether active noise cancellation is supported." },
            new CanonicalAttributeDefinition { Key = "battery_life_hours", DisplayName = "Battery Life", ValueType = "decimal", Unit = "hour", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Advertised wireless battery life in hours." },
            new CanonicalAttributeDefinition { Key = "case_battery_life_hours", DisplayName = "Case Battery Life", ValueType = "decimal", Unit = "hour", ConflictSensitivity = ConflictSensitivity.Low, Description = "Combined battery life in hours when a charging case is included." },
            new CanonicalAttributeDefinition { Key = "charging_port", DisplayName = "Charging Port", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Low, Description = "Primary wired charging connector." },
            new CanonicalAttributeDefinition { Key = "driver_size_mm", DisplayName = "Driver Size", ValueType = "decimal", Unit = "mm", ConflictSensitivity = ConflictSensitivity.Low, Description = "Driver size in millimetres when published." },
            new CanonicalAttributeDefinition { Key = "impedance_ohm", DisplayName = "Impedance", ValueType = "integer", Unit = "ohm", ConflictSensitivity = ConflictSensitivity.Low, Description = "Nominal impedance in ohms when published." },
            new CanonicalAttributeDefinition { Key = "microphone", DisplayName = "Microphone", ValueType = "boolean", ConflictSensitivity = ConflictSensitivity.Low, Description = "Whether an integrated microphone is present." },
            new CanonicalAttributeDefinition { Key = "ip_rating", DisplayName = "IP Rating", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Low, Description = "Advertised ingress protection rating such as IPX4 or IP54." },
            new CanonicalAttributeDefinition { Key = "weight_g", DisplayName = "Weight", ValueType = "integer", Unit = "g", ConflictSensitivity = ConflictSensitivity.Low, Description = "Product weight in grams." }
        ]
    };

    public string SupportedCategoryKey => CategoryKey;

    public CategorySchema GetSchema()
    {
        return HeadphonesSchema;
    }
}