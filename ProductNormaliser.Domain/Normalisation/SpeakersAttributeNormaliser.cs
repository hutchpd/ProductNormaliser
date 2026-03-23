using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Core.Normalisation;

public sealed class SpeakersAttributeNormaliser : CategoryAttributeNormaliserBase
{
    public SpeakersAttributeNormaliser(
        AttributeNameNormaliser? attributeNameNormaliser = null,
        MeasurementParser? measurementParser = null,
        UnitConversionService? unitConversionService = null,
        IUnmappedAttributeRecorder? unmappedAttributeRecorder = null)
        : base(
            SpeakersCategorySchemaProvider.CategoryKey,
            new SpeakersCategorySchemaProvider().GetSchema(),
            identityAttributeKeys: ["gtin", "brand", "model_number", "speaker_type", "connection_type"],
            completenessAttributeKeys: ["brand", "model_number", "speaker_type", "connection_type", "wireless", "battery_life_hours", "power_output_w", "voice_assistant"],
            aliases: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["speaker type"] = "speaker_type",
                ["type"] = "speaker_type",
                ["connectivity"] = "connection_type",
                ["connection"] = "connection_type",
                ["wireless"] = "wireless",
                ["battery life"] = "battery_life_hours",
                ["power"] = "power_output_w",
                ["power output"] = "power_output_w",
                ["assistant"] = "voice_assistant",
                ["voice control"] = "voice_assistant",
                ["water resistant"] = "water_resistant",
                ["weight"] = "weight_g"
            },
            valueMappings: new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
            {
                ["connection_type"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["bluetooth"] = "Bluetooth",
                    ["wi-fi"] = "Wi-Fi",
                    ["wifi"] = "Wi-Fi",
                    ["wired"] = "Wired"
                },
                ["speaker_type"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["portable bluetooth speaker"] = "Portable Bluetooth",
                    ["smart speaker"] = "Smart Speaker",
                    ["bookshelf"] = "Bookshelf"
                },
                ["voice_assistant"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["alexa"] = "Alexa",
                    ["google assistant"] = "Google Assistant",
                    ["siri"] = "Siri"
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
            "wireless" or "water_resistant" => NormaliseBoolean(rawAttribute, definition),
            "battery_life_hours" => NormaliseDecimalMeasurement(rawAttribute, definition),
            "power_output_w" or "weight_g" => NormaliseIntegerMeasurement(rawAttribute, definition),
            _ => default!
        };

        return normalisedValue is not null;
    }
}