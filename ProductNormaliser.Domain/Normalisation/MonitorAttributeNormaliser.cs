using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Core.Normalisation;

public sealed class MonitorAttributeNormaliser : CategoryAttributeNormaliserBase
{
    public MonitorAttributeNormaliser(
        AttributeNameNormaliser? attributeNameNormaliser = null,
        MeasurementParser? measurementParser = null,
        UnitConversionService? unitConversionService = null,
        IUnmappedAttributeRecorder? unmappedAttributeRecorder = null)
        : base(
            MonitorCategorySchemaProvider.CategoryKey,
            new MonitorCategorySchemaProvider().GetSchema(),
            identityAttributeKeys: ["brand", "model_number", "screen_size_inch", "native_resolution", "panel_type"],
            completenessAttributeKeys: ["brand", "model_number", "screen_size_inch", "native_resolution", "panel_type", "refresh_rate_hz"],
            aliases: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["screen size"] = "screen_size_inch",
                ["display size"] = "screen_size_inch",
                ["panel technology"] = "panel_type",
                ["panel type"] = "panel_type",
                ["refresh rate"] = "refresh_rate_hz",
                ["hdmi ports"] = "hdmi_port_count",
                ["displayport ports"] = "displayport_port_count",
                ["displayport inputs"] = "displayport_port_count"
            },
            valueMappings: new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
            {
                ["panel_type"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ips"] = "IPS",
                    ["va"] = "VA",
                    ["tn"] = "TN",
                    ["oled"] = "OLED"
                },
                ["native_resolution"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["uhd"] = "4K",
                    ["ultra hd"] = "4K",
                    ["qhd"] = "1440p",
                    ["full hd"] = "1080p"
                }
            },
            attributeNameNormaliser: attributeNameNormaliser,
            measurementParser: measurementParser,
            unitConversionService: unitConversionService,
            unmappedAttributeRecorder: unmappedAttributeRecorder)
    {
    }

    protected override bool TryNormaliseKnownAttribute(SourceAttributeValue rawAttribute, CanonicalAttributeDefinition definition, out NormalisedAttributeValue normalisedValue)
    {
        normalisedValue = definition.Key switch
        {
            "screen_size_inch" => NormaliseScreenSize(rawAttribute, definition),
            "refresh_rate_hz" => NormaliseIntegerMeasurement(rawAttribute, definition),
            "hdmi_port_count" => NormaliseInteger(rawAttribute, definition),
            "displayport_port_count" => NormaliseInteger(rawAttribute, definition),
            "width_mm" or "height_mm" or "depth_mm" => NormaliseDecimalMeasurement(rawAttribute, definition),
            _ => default!
        };

        return normalisedValue is not null;
    }
}