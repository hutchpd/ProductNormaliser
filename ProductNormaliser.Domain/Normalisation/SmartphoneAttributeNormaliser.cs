using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Core.Normalisation;

public sealed class SmartphoneAttributeNormaliser : CategoryAttributeNormaliserBase
{
    public SmartphoneAttributeNormaliser(
        AttributeNameNormaliser? attributeNameNormaliser = null,
        MeasurementParser? measurementParser = null,
        UnitConversionService? unitConversionService = null,
        IUnmappedAttributeRecorder? unmappedAttributeRecorder = null)
        : base(
            SmartphoneCategorySchemaProvider.CategoryKey,
            new SmartphoneCategorySchemaProvider().GetSchema(),
            identityAttributeKeys: ["gtin", "brand", "model_number", "screen_size_inch", "storage_capacity_gb"],
            completenessAttributeKeys: ["brand", "model_number", "screen_size_inch", "native_resolution", "storage_capacity_gb", "ram_gb", "operating_system", "cellular_generation", "rear_camera_mp"],
            aliases: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["screen size"] = "screen_size_inch",
                ["display size"] = "screen_size_inch",
                ["screen diagonal"] = "screen_size_inch",
                ["storage"] = "storage_capacity_gb",
                ["storage capacity"] = "storage_capacity_gb",
                ["memory"] = "ram_gb",
                ["ram"] = "ram_gb",
                ["os"] = "operating_system",
                ["network"] = "cellular_generation",
                ["mobile network"] = "cellular_generation",
                ["main camera"] = "rear_camera_mp",
                ["rear camera"] = "rear_camera_mp",
                ["battery"] = "battery_capacity_mah",
                ["battery capacity"] = "battery_capacity_mah",
                ["dual sim"] = "dual_sim"
            },
            valueMappings: new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
            {
                ["operating_system"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["android"] = "Android",
                    ["ios"] = "iOS"
                },
                ["cellular_generation"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["5g"] = "5G",
                    ["4g"] = "4G",
                    ["lte"] = "4G"
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
            "storage_capacity_gb" or "ram_gb" or "battery_capacity_mah" => NormaliseIntegerMeasurement(rawAttribute, definition),
            "rear_camera_mp" => NormaliseDecimalMeasurement(rawAttribute, definition),
            "dual_sim" => NormaliseBoolean(rawAttribute, definition),
            _ => default!
        };

        return normalisedValue is not null;
    }
}