using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Core.Normalisation;

public sealed class TabletAttributeNormaliser : CategoryAttributeNormaliserBase
{
    public TabletAttributeNormaliser(
        AttributeNameNormaliser? attributeNameNormaliser = null,
        MeasurementParser? measurementParser = null,
        UnitConversionService? unitConversionService = null,
        IUnmappedAttributeRecorder? unmappedAttributeRecorder = null)
        : base(
            TabletCategorySchemaProvider.CategoryKey,
            new TabletCategorySchemaProvider().GetSchema(),
            identityAttributeKeys: ["gtin", "brand", "model_number", "display_size_inch", "storage_capacity_gb"],
            completenessAttributeKeys: ["brand", "model_number", "display_size_inch", "native_resolution", "storage_capacity_gb", "ram_gb", "operating_system", "connectivity"],
            aliases: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["screen size"] = "display_size_inch",
                ["display size"] = "display_size_inch",
                ["screen diagonal"] = "display_size_inch",
                ["storage"] = "storage_capacity_gb",
                ["storage capacity"] = "storage_capacity_gb",
                ["memory"] = "ram_gb",
                ["ram"] = "ram_gb",
                ["os"] = "operating_system",
                ["platform"] = "operating_system",
                ["network"] = "connectivity",
                ["connectivity"] = "connectivity",
                ["battery life"] = "battery_life_hours",
                ["weight"] = "weight_g",
                ["stylus"] = "stylus_support",
                ["pen support"] = "stylus_support"
            },
            valueMappings: new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
            {
                ["operating_system"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ipados"] = "iPadOS",
                    ["android"] = "Android",
                    ["windows 11"] = "Windows 11",
                    ["fire os"] = "Fire OS"
                },
                ["connectivity"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["wifi"] = "Wi-Fi",
                    ["wi-fi"] = "Wi-Fi",
                    ["wifi + cellular"] = "Wi-Fi + Cellular",
                    ["wi-fi + cellular"] = "Wi-Fi + Cellular",
                    ["5g"] = "5G",
                    ["4g"] = "4G"
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
            "display_size_inch" => NormaliseScreenSize(rawAttribute, definition),
            "storage_capacity_gb" or "ram_gb" or "weight_g" => NormaliseIntegerMeasurement(rawAttribute, definition),
            "battery_life_hours" => NormaliseDecimalMeasurement(rawAttribute, definition),
            "stylus_support" => NormaliseBoolean(rawAttribute, definition),
            _ => default!
        };

        return normalisedValue is not null;
    }
}