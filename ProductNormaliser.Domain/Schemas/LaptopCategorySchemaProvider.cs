using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Schemas;

public sealed class LaptopCategorySchemaProvider : ICategorySchemaProvider
{
    public const string CategoryKey = "laptop";

    private static readonly CategorySchema LaptopSchema = new()
    {
        CategoryKey = CategoryKey,
        DisplayName = "Laptops",
        Attributes =
        [
            new CanonicalAttributeDefinition { Key = "brand", DisplayName = "Brand", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.Critical, Description = "Manufacturer brand name." },
            new CanonicalAttributeDefinition { Key = "model_number", DisplayName = "Model Number", ValueType = "string", IsRequired = true, ConflictSensitivity = ConflictSensitivity.Critical, Description = "Manufacturer model identifier." },
            new CanonicalAttributeDefinition { Key = "gtin", DisplayName = "GTIN", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Critical, Description = "Global trade item number when published." },
            new CanonicalAttributeDefinition { Key = "cpu_model", DisplayName = "CPU Model", ValueType = "string", ConflictSensitivity = ConflictSensitivity.High, Description = "Processor model." },
            new CanonicalAttributeDefinition { Key = "ram_gb", DisplayName = "RAM", ValueType = "integer", Unit = "gb", ConflictSensitivity = ConflictSensitivity.High, Description = "Installed memory in gigabytes." },
            new CanonicalAttributeDefinition { Key = "storage_capacity_gb", DisplayName = "Storage Capacity", ValueType = "integer", Unit = "gb", ConflictSensitivity = ConflictSensitivity.High, Description = "Primary storage capacity in gigabytes." },
            new CanonicalAttributeDefinition { Key = "storage_type", DisplayName = "Storage Type", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Primary storage technology such as SSD or HDD." },
            new CanonicalAttributeDefinition { Key = "display_size_inch", DisplayName = "Display Size", ValueType = "decimal", Unit = "inch", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Nominal display size in inches." },
            new CanonicalAttributeDefinition { Key = "native_resolution", DisplayName = "Native Resolution", ValueType = "string", ConflictSensitivity = ConflictSensitivity.High, Description = "Display native resolution." },
            new CanonicalAttributeDefinition { Key = "graphics_model", DisplayName = "Graphics Model", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Medium, Description = "Integrated or discrete graphics model." },
            new CanonicalAttributeDefinition { Key = "operating_system", DisplayName = "Operating System", ValueType = "string", ConflictSensitivity = ConflictSensitivity.Low, Description = "Preinstalled operating system." },
            new CanonicalAttributeDefinition { Key = "battery_life_hours", DisplayName = "Battery Life", ValueType = "decimal", Unit = "hour", ConflictSensitivity = ConflictSensitivity.Low, Description = "Advertised battery life in hours." },
            new CanonicalAttributeDefinition { Key = "weight_kg", DisplayName = "Weight", ValueType = "decimal", Unit = "kg", ConflictSensitivity = ConflictSensitivity.Low, Description = "Product weight in kilograms." }
        ]
    };

    public string SupportedCategoryKey => CategoryKey;

    public CategorySchema GetSchema()
    {
        return LaptopSchema;
    }
}