using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Schemas;

public sealed class TvCategorySchemaProvider : ICategorySchemaProvider
{
    public const string CategoryKey = "tv";

    private static readonly CategorySchema TvSchema = new()
    {
        CategoryKey = CategoryKey,
        DisplayName = "Televisions",
        Attributes =
        [
            new CanonicalAttributeDefinition
            {
                Key = "brand",
                DisplayName = "Brand",
                ValueType = "string",
                IsRequired = true,
                Description = "Manufacturer brand name."
            },
            new CanonicalAttributeDefinition
            {
                Key = "model_number",
                DisplayName = "Model Number",
                ValueType = "string",
                IsRequired = true,
                Description = "Manufacturer model identifier."
            },
            new CanonicalAttributeDefinition
            {
                Key = "gtin",
                DisplayName = "GTIN",
                ValueType = "string",
                Description = "Global trade item number when published."
            },
            new CanonicalAttributeDefinition
            {
                Key = "screen_size_inch",
                DisplayName = "Screen Size",
                ValueType = "decimal",
                Unit = "inch",
                Description = "Nominal display size in inches."
            },
            new CanonicalAttributeDefinition
            {
                Key = "native_resolution",
                DisplayName = "Native Resolution",
                ValueType = "string",
                Description = "Panel native resolution, for example 3840x2160."
            },
            new CanonicalAttributeDefinition
            {
                Key = "display_technology",
                DisplayName = "Display Technology",
                ValueType = "string",
                Description = "Panel technology such as OLED, QLED, or Mini LED."
            },
            new CanonicalAttributeDefinition
            {
                Key = "hdmi_port_count",
                DisplayName = "HDMI Port Count",
                ValueType = "integer",
                Description = "Number of HDMI inputs."
            },
            new CanonicalAttributeDefinition
            {
                Key = "smart_tv",
                DisplayName = "Smart TV",
                ValueType = "boolean",
                Description = "Whether the television includes a smart TV platform."
            },
            new CanonicalAttributeDefinition
            {
                Key = "smart_platform",
                DisplayName = "Smart Platform",
                ValueType = "string",
                Description = "Smart TV operating system or platform."
            },
            new CanonicalAttributeDefinition
            {
                Key = "refresh_rate_hz",
                DisplayName = "Refresh Rate",
                ValueType = "integer",
                Unit = "hz",
                Description = "Advertised panel refresh rate in hertz."
            },
            new CanonicalAttributeDefinition
            {
                Key = "vesa_mount_width_mm",
                DisplayName = "VESA Mount Width",
                ValueType = "integer",
                Unit = "mm",
                Description = "Horizontal VESA mounting distance in millimetres."
            },
            new CanonicalAttributeDefinition
            {
                Key = "vesa_mount_height_mm",
                DisplayName = "VESA Mount Height",
                ValueType = "integer",
                Unit = "mm",
                Description = "Vertical VESA mounting distance in millimetres."
            },
            new CanonicalAttributeDefinition
            {
                Key = "width_mm",
                DisplayName = "Width",
                ValueType = "decimal",
                Unit = "mm",
                Description = "Product width in millimetres."
            },
            new CanonicalAttributeDefinition
            {
                Key = "height_mm",
                DisplayName = "Height",
                ValueType = "decimal",
                Unit = "mm",
                Description = "Product height in millimetres."
            },
            new CanonicalAttributeDefinition
            {
                Key = "depth_mm",
                DisplayName = "Depth",
                ValueType = "decimal",
                Unit = "mm",
                Description = "Product depth in millimetres."
            }
        ]
    };

    public string SupportedCategoryKey => CategoryKey;

    public CategorySchema GetSchema()
    {
        return TvSchema;
    }
}