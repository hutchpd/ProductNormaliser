using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Core.Normalisation;

public sealed class TvAttributeNormaliser : CategoryAttributeNormaliserBase
{
    public TvAttributeNormaliser(
        AttributeNameNormaliser? attributeNameNormaliser = null,
        MeasurementParser? measurementParser = null,
        UnitConversionService? unitConversionService = null,
        IUnmappedAttributeRecorder? unmappedAttributeRecorder = null)
        : base(
            TvCategorySchemaProvider.CategoryKey,
            new TvCategorySchemaProvider().GetSchema(),
            identityAttributeKeys: ["gtin", "brand", "model_number", "screen_size_inch", "native_resolution"],
            completenessAttributeKeys: ["brand", "model_number", "screen_size_inch", "native_resolution", "display_technology"],
            aliases: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["screen size"] = "screen_size_inch",
                ["screensize"] = "screen_size_inch",
                ["display size"] = "screen_size_inch",
                ["hdmi ports"] = "hdmi_port_count",
                ["number of hdmi ports"] = "hdmi_port_count",
                ["smart tv"] = "smart_tv",
                ["refresh rate"] = "refresh_rate_hz"
            },
            valueMappings: new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
            {
                ["display_technology"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["oled"] = "OLED",
                    ["qled"] = "QLED",
                    ["led"] = "LED"
                },
                ["native_resolution"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["4k ultra hd"] = "4K",
                    ["ultra hd"] = "4K",
                    ["full hd"] = "1080p",
                    ["hd ready"] = "720p"
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
            "smart_tv" => NormaliseBoolean(rawAttribute, definition),
            "width_mm" or "height_mm" or "depth_mm" or "vesa_mount_width_mm" or "vesa_mount_height_mm" => NormaliseDecimalMeasurement(rawAttribute, definition),
            _ => default!
        };

        return normalisedValue is not null;
    }
}