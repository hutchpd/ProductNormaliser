using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Schemas;

public sealed class MonitorCategorySchemaProvider : ICategorySchemaProvider
{
    public const string CategoryKey = "monitor";

    private static readonly CategorySchema MonitorSchema = new()
    {
        CategoryKey = CategoryKey,
        DisplayName = "Monitors",
        Attributes =
        [
            new CanonicalAttributeDefinition { Key = "brand", DisplayName = "Brand", ValueType = "string", IsRequired = true, Description = "Manufacturer brand name." },
            new CanonicalAttributeDefinition { Key = "model_number", DisplayName = "Model Number", ValueType = "string", IsRequired = true, Description = "Manufacturer model identifier." },
            new CanonicalAttributeDefinition { Key = "screen_size_inch", DisplayName = "Screen Size", ValueType = "decimal", Unit = "inch", Description = "Nominal display size in inches." },
            new CanonicalAttributeDefinition { Key = "native_resolution", DisplayName = "Native Resolution", ValueType = "string", Description = "Panel native resolution." },
            new CanonicalAttributeDefinition { Key = "panel_type", DisplayName = "Panel Type", ValueType = "string", Description = "Panel technology such as IPS, VA, or OLED." },
            new CanonicalAttributeDefinition { Key = "refresh_rate_hz", DisplayName = "Refresh Rate", ValueType = "integer", Unit = "hz", Description = "Advertised refresh rate in hertz." },
            new CanonicalAttributeDefinition { Key = "hdmi_port_count", DisplayName = "HDMI Port Count", ValueType = "integer", Description = "Number of HDMI inputs." },
            new CanonicalAttributeDefinition { Key = "displayport_port_count", DisplayName = "DisplayPort Count", ValueType = "integer", Description = "Number of DisplayPort inputs." },
            new CanonicalAttributeDefinition { Key = "width_mm", DisplayName = "Width", ValueType = "decimal", Unit = "mm", Description = "Product width in millimetres." },
            new CanonicalAttributeDefinition { Key = "height_mm", DisplayName = "Height", ValueType = "decimal", Unit = "mm", Description = "Product height in millimetres." },
            new CanonicalAttributeDefinition { Key = "depth_mm", DisplayName = "Depth", ValueType = "decimal", Unit = "mm", Description = "Product depth in millimetres." }
        ]
    };

    public string SupportedCategoryKey => CategoryKey;

    public CategorySchema GetSchema()
    {
        return MonitorSchema;
    }
}