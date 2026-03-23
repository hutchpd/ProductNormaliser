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
            new CanonicalAttributeDefinition { Key = "gtin", DisplayName = "GTIN", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Critical, Description = "Global trade item number when published." },
            new CanonicalAttributeDefinition { Key = "form_factor", DisplayName = "Form Factor", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.High, Description = "Headphone form factor such as over-ear or in-ear." },
            new CanonicalAttributeDefinition { Key = "connection_type", DisplayName = "Connection Type", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.High, Description = "Primary audio connection such as wired or Bluetooth." },
            new CanonicalAttributeDefinition { Key = "wireless", DisplayName = "Wireless", ValueType = "boolean", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Whether the headphones operate wirelessly." },
            new CanonicalAttributeDefinition { Key = "noise_cancelling", DisplayName = "Noise Cancelling", ValueType = "boolean", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Whether active noise cancellation is supported." },
            new CanonicalAttributeDefinition { Key = "battery_life_hours", DisplayName = "Battery Life", ValueType = "decimal", Unit = "hour", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Advertised wireless battery life in hours." },
            new CanonicalAttributeDefinition { Key = "driver_size_mm", DisplayName = "Driver Size", ValueType = "decimal", Unit = "mm", ConflictSensitivity = ConflictSensitivity.Low, Description = "Driver size in millimetres when published." },
            new CanonicalAttributeDefinition { Key = "microphone", DisplayName = "Microphone", ValueType = "boolean", ConflictSensitivity = ConflictSensitivity.Low, Description = "Whether an integrated microphone is present." },
            new CanonicalAttributeDefinition { Key = "weight_g", DisplayName = "Weight", ValueType = "integer", Unit = "g", ConflictSensitivity = ConflictSensitivity.Low, Description = "Product weight in grams." }
        ]
    };

    public string SupportedCategoryKey => CategoryKey;

    public CategorySchema GetSchema()
    {
        return HeadphonesSchema;
    }
}