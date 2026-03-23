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
            new CanonicalAttributeDefinition { Key = "gtin", DisplayName = "GTIN", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Critical, Description = "Global trade item number when published." },
            new CanonicalAttributeDefinition { Key = "speaker_type", DisplayName = "Speaker Type", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.High, Description = "Primary speaker type such as bookshelf, smart speaker, or portable Bluetooth." },
            new CanonicalAttributeDefinition { Key = "connection_type", DisplayName = "Connection Type", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.High, Description = "Primary audio connection such as wired, Bluetooth, or Wi-Fi." },
            new CanonicalAttributeDefinition { Key = "wireless", DisplayName = "Wireless", ValueType = "boolean", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Whether the speaker supports wireless playback." },
            new CanonicalAttributeDefinition { Key = "battery_life_hours", DisplayName = "Battery Life", ValueType = "decimal", Unit = "hour", ConflictSensitivity = ConflictSensitivity.Low, Description = "Advertised battery life in hours for portable models." },
            new CanonicalAttributeDefinition { Key = "power_output_w", DisplayName = "Power Output", ValueType = "integer", Unit = "w", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Advertised power output in watts." },
            new CanonicalAttributeDefinition { Key = "voice_assistant", DisplayName = "Voice Assistant", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Low, Description = "Built-in voice assistant platform when present." },
            new CanonicalAttributeDefinition { Key = "water_resistant", DisplayName = "Water Resistant", ValueType = "boolean", ConflictSensitivity = ConflictSensitivity.Low, Description = "Whether ingress resistance is advertised." },
            new CanonicalAttributeDefinition { Key = "weight_g", DisplayName = "Weight", ValueType = "integer", Unit = "g", ConflictSensitivity = ConflictSensitivity.Low, Description = "Product weight in grams." }
        ]
    };

    public string SupportedCategoryKey => CategoryKey;

    public CategorySchema GetSchema()
    {
        return SpeakersSchema;
    }
}