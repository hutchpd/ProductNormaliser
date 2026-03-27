using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Schemas;

public sealed class SpeakersCategorySchemaProvider : ICategorySchemaProvider
{
    public const string CategoryKey = "speakers";

    private static readonly CategorySchema SpeakersSchema = new()
    {
        CategoryKey = CategoryKey,
        DisplayName = "Speakers",
        Attributes =
        [
            new CanonicalAttributeDefinition { Key = "brand", DisplayName = "Brand", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.Critical, Description = "Manufacturer brand name." },
            new CanonicalAttributeDefinition { Key = "model_number", DisplayName = "Model Number", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.Critical, Description = "Manufacturer model identifier." },
            new CanonicalAttributeDefinition { Key = "model_family", DisplayName = "Model Family", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.High, Description = "Commercial model family such as Sonos Move or JBL Flip." },
            new CanonicalAttributeDefinition { Key = "variant_name", DisplayName = "Variant Name", ValueType = "string", ConflictSensitivity = ConflictSensitivity.High, Description = "Sellable variant label such as Gen 2, Pro, or Portable." },
            new CanonicalAttributeDefinition { Key = "manufacturer_part_number", DisplayName = "Manufacturer Part Number", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Critical, Description = "Manufacturer SKU or part number for the specific speaker variant." },
            new CanonicalAttributeDefinition { Key = "gtin", DisplayName = "GTIN", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Critical, Description = "Global trade item number when published." },
            new CanonicalAttributeDefinition { Key = "release_year", DisplayName = "Release Year", ValueType = "integer", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Commercial release year for the product family or variant." },
            new CanonicalAttributeDefinition { Key = "colour", DisplayName = "Colour", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Manufacturer colour or finish name for the sellable variant." },
            new CanonicalAttributeDefinition { Key = "speaker_type", DisplayName = "Speaker Type", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.High, Description = "Primary speaker type such as bookshelf, smart speaker, or portable Bluetooth." },
            new CanonicalAttributeDefinition { Key = "connection_type", DisplayName = "Connection Type", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.High, Description = "Primary audio connection such as wired, Bluetooth, or Wi-Fi." },
            new CanonicalAttributeDefinition { Key = "wireless", DisplayName = "Wireless", ValueType = "boolean", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Whether the speaker supports wireless playback." },
            new CanonicalAttributeDefinition { Key = "bluetooth_version", DisplayName = "Bluetooth Version", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Low, Description = "Advertised Bluetooth version for wireless models." },
            new CanonicalAttributeDefinition { Key = "battery_life_hours", DisplayName = "Battery Life", ValueType = "decimal", Unit = "hour", ConflictSensitivity = ConflictSensitivity.Low, Description = "Advertised battery life in hours for portable models." },
            new CanonicalAttributeDefinition { Key = "power_output_w", DisplayName = "Power Output", ValueType = "integer", Unit = "w", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Advertised power output in watts." },
            new CanonicalAttributeDefinition { Key = "voice_assistant", DisplayName = "Voice Assistant", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Low, Description = "Built-in voice assistant platform when present." },
            new CanonicalAttributeDefinition { Key = "smart_platform", DisplayName = "Smart Platform", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Low, Description = "Built-in software platform or smart-speaker ecosystem." },
            new CanonicalAttributeDefinition { Key = "water_resistant", DisplayName = "Water Resistant", ValueType = "boolean", ConflictSensitivity = ConflictSensitivity.Low, Description = "Whether ingress resistance is advertised." },
            new CanonicalAttributeDefinition { Key = "ip_rating", DisplayName = "IP Rating", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Low, Description = "Advertised ingress protection rating such as IP67." },
            new CanonicalAttributeDefinition { Key = "stereo_pairing", DisplayName = "Stereo Pairing", ValueType = "boolean", ConflictSensitivity = ConflictSensitivity.Low, Description = "Whether two identical units can be paired for stereo playback." },
            new CanonicalAttributeDefinition { Key = "multiroom_support", DisplayName = "Multiroom Support", ValueType = "boolean", ConflictSensitivity = ConflictSensitivity.Low, Description = "Whether the speaker supports multiroom or grouped playback." },
            new CanonicalAttributeDefinition { Key = "charging_port", DisplayName = "Charging Port", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Low, Description = "Primary wired charging connector for portable models." },
            new CanonicalAttributeDefinition { Key = "width_mm", DisplayName = "Width", ValueType = "decimal", Unit = "mm", ConflictSensitivity = ConflictSensitivity.Low, Description = "Product width in millimetres." },
            new CanonicalAttributeDefinition { Key = "height_mm", DisplayName = "Height", ValueType = "decimal", Unit = "mm", ConflictSensitivity = ConflictSensitivity.Low, Description = "Product height in millimetres." },
            new CanonicalAttributeDefinition { Key = "depth_mm", DisplayName = "Depth", ValueType = "decimal", Unit = "mm", ConflictSensitivity = ConflictSensitivity.Low, Description = "Product depth in millimetres." },
            new CanonicalAttributeDefinition { Key = "weight_g", DisplayName = "Weight", ValueType = "integer", Unit = "g", ConflictSensitivity = ConflictSensitivity.Low, Description = "Product weight in grams." }
        ]
    };

    public string SupportedCategoryKey => CategoryKey;

    public CategorySchema GetSchema()
    {
        return SpeakersSchema;
    }
}