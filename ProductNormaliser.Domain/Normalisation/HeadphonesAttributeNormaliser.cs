using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Core.Normalisation;

public sealed class HeadphonesAttributeNormaliser : CategoryAttributeNormaliserBase
{
    public HeadphonesAttributeNormaliser(
        AttributeNameNormaliser? attributeNameNormaliser = null,
        MeasurementParser? measurementParser = null,
        UnitConversionService? unitConversionService = null,
        IUnmappedAttributeRecorder? unmappedAttributeRecorder = null)
        : base(
            HeadphonesCategorySchemaProvider.CategoryKey,
            new HeadphonesCategorySchemaProvider().GetSchema(),
            identityAttributeKeys: ["gtin", "brand", "model_number", "form_factor", "connection_type"],
            completenessAttributeKeys: ["brand", "model_number", "form_factor", "connection_type", "wireless", "noise_cancelling", "battery_life_hours", "driver_size_mm"],
            aliases: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["type"] = "form_factor",
                ["fit"] = "form_factor",
                ["headphone type"] = "form_factor",
                ["connectivity"] = "connection_type",
                ["connection"] = "connection_type",
                ["wireless"] = "wireless",
                ["noise cancelling"] = "noise_cancelling",
                ["anc"] = "noise_cancelling",
                ["battery life"] = "battery_life_hours",
                ["driver size"] = "driver_size_mm",
                ["microphone"] = "microphone",
                ["weight"] = "weight_g"
            },
            valueMappings: new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
            {
                ["form_factor"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["over-ear"] = "Over-Ear",
                    ["on-ear"] = "On-Ear",
                    ["in-ear"] = "In-Ear"
                },
                ["connection_type"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["bluetooth"] = "Bluetooth",
                    ["wired"] = "Wired",
                    ["usb-c"] = "USB-C",
                    ["3.5mm"] = "3.5 mm"
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
            "wireless" or "noise_cancelling" or "microphone" => NormaliseBoolean(rawAttribute, definition),
            "battery_life_hours" or "driver_size_mm" => NormaliseDecimalMeasurement(rawAttribute, definition),
            "weight_g" => NormaliseIntegerMeasurement(rawAttribute, definition),
            _ => default!
        };

        return normalisedValue is not null;
    }
}