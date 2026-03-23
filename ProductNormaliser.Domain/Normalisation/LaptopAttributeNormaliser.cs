using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Core.Normalisation;

public sealed class LaptopAttributeNormaliser : CategoryAttributeNormaliserBase
{
    public LaptopAttributeNormaliser(
        AttributeNameNormaliser? attributeNameNormaliser = null,
        MeasurementParser? measurementParser = null,
        UnitConversionService? unitConversionService = null,
        IUnmappedAttributeRecorder? unmappedAttributeRecorder = null)
        : base(
            LaptopCategorySchemaProvider.CategoryKey,
            new LaptopCategorySchemaProvider().GetSchema(),
            identityAttributeKeys: ["gtin", "brand", "model_number", "cpu_model", "display_size_inch", "storage_capacity_gb"],
            completenessAttributeKeys: ["brand", "model_number", "cpu_model", "ram_gb", "storage_capacity_gb", "storage_type", "display_size_inch", "native_resolution", "operating_system"],
            aliases: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["processor"] = "cpu_model",
                ["cpu"] = "cpu_model",
                ["processor model"] = "cpu_model",
                ["memory"] = "ram_gb",
                ["ram"] = "ram_gb",
                ["ram memory"] = "ram_gb",
                ["storage"] = "storage_capacity_gb",
                ["ssd"] = "storage_capacity_gb",
                ["ssd capacity"] = "storage_capacity_gb",
                ["storage capacity"] = "storage_capacity_gb",
                ["drive type"] = "storage_type",
                ["display size"] = "display_size_inch",
                ["screen size"] = "display_size_inch",
                ["screen resolution"] = "native_resolution",
                ["gpu"] = "graphics_model",
                ["graphics"] = "graphics_model",
                ["graphics card"] = "graphics_model",
                ["os"] = "operating_system",
                ["battery life"] = "battery_life_hours",
                ["weight"] = "weight_kg"
            },
            valueMappings: new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
            {
                ["storage_type"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["solid state drive"] = "SSD",
                    ["ssd"] = "SSD",
                    ["hard disk drive"] = "HDD",
                    ["hdd"] = "HDD"
                },
                ["operating_system"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["windows 11 home"] = "Windows 11 Home",
                    ["windows 11 pro"] = "Windows 11 Pro",
                    ["macos"] = "macOS",
                    ["chrome os"] = "ChromeOS"
                },
                ["native_resolution"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["full hd"] = "1080p",
                    ["fhd"] = "1080p",
                    ["qhd"] = "1440p",
                    ["uhd"] = "4K",
                    ["3840 x 2160"] = "4K",
                    ["2560 x 1600"] = "1600p",
                    ["1920 x 1080"] = "1080p"
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
            "ram_gb" => NormaliseIntegerMeasurement(rawAttribute, definition),
            "storage_capacity_gb" => NormaliseIntegerMeasurement(rawAttribute, definition),
            "display_size_inch" => NormaliseScreenSize(rawAttribute, definition),
            "battery_life_hours" => NormaliseDecimalMeasurement(rawAttribute, definition),
            "weight_kg" => NormaliseDecimalMeasurement(rawAttribute, definition),
            _ => default!
        };

        return normalisedValue is not null;
    }
}