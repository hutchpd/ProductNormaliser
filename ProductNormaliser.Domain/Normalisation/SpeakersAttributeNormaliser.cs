using System.Globalization;
using System.Text.RegularExpressions;
using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Core.Normalisation;

public sealed class SpeakersAttributeNormaliser : CategoryAttributeNormaliserBase
{
    private static readonly Regex LooseNumberPattern = new(@"(?<value>\d+(?:[\.,]\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex IpRatingPattern = new(@"\bip\s*[- ]?\s*(?<rating>[a-z0-9]{2,4})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public SpeakersAttributeNormaliser(
        AttributeNameNormaliser? attributeNameNormaliser = null,
        MeasurementParser? measurementParser = null,
        UnitConversionService? unitConversionService = null,
        IUnmappedAttributeRecorder? unmappedAttributeRecorder = null)
        : base(
            SpeakersCategorySchemaProvider.CategoryKey,
            new SpeakersCategorySchemaProvider().GetSchema(),
            identityAttributeKeys:
            [
                "gtin",
                "brand",
                "model_number",
                "model_family",
                "variant_name",
                "manufacturer_part_number",
                "colour",
                "speaker_type",
                "connection_type"
            ],
            completenessAttributeKeys:
            [
                "brand",
                "model_number",
                "model_family",
                "speaker_type",
                "connection_type",
                "wireless",
                "bluetooth_version",
                "battery_life_hours",
                "power_output_w",
                "voice_assistant",
                "smart_platform",
                "ip_rating",
                "stereo_pairing",
                "multiroom_support"
            ],
            aliases: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["speaker type"] = "speaker_type",
                ["speaker category"] = "speaker_type",
                ["type"] = "speaker_type",
                ["connectivity"] = "connection_type",
                ["connection"] = "connection_type",
                ["wireless connectivity"] = "connection_type",
                ["wireless"] = "wireless",
                ["bluetooth"] = "bluetooth_version",
                ["bluetooth ver"] = "bluetooth_version",
                ["bluetooth version"] = "bluetooth_version",
                ["bt version"] = "bluetooth_version",
                ["battery life"] = "battery_life_hours",
                ["power"] = "power_output_w",
                ["power output"] = "power_output_w",
                ["assistant"] = "voice_assistant",
                ["voice control"] = "voice_assistant",
                ["voice assistant"] = "voice_assistant",
                ["platform"] = "smart_platform",
                ["water resistant"] = "water_resistant",
                ["waterproof rating"] = "ip_rating",
                ["protection rating"] = "ip_rating",
                ["stereo pair"] = "stereo_pairing",
                ["stereo pairing"] = "stereo_pairing",
                ["multiroom"] = "multiroom_support",
                ["multi-room"] = "multiroom_support",
                ["charging port"] = "charging_port",
                ["charging interface"] = "charging_port",
                ["thickness"] = "depth_mm",
                ["weight"] = "weight_g",
                ["color"] = "colour"
            },
            valueMappings: new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
            {
                ["connection_type"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["bluetooth"] = "Bluetooth",
                    ["wireless bluetooth"] = "Bluetooth",
                    ["wi-fi"] = "Wi-Fi",
                    ["wifi"] = "Wi-Fi",
                    ["wired"] = "Wired"
                },
                ["speaker_type"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["portable bluetooth speaker"] = "Portable Bluetooth",
                    ["portable speaker"] = "Portable Bluetooth",
                    ["smart speaker"] = "Smart Speaker",
                    ["bookshelf"] = "Bookshelf",
                    ["bookshelf speaker"] = "Bookshelf",
                    ["party speaker"] = "Party Speaker",
                    ["soundbar"] = "Soundbar"
                },
                ["voice_assistant"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["alexa"] = "Alexa",
                    ["amazon alexa"] = "Alexa",
                    ["google assistant"] = "Google Assistant",
                    ["siri"] = "Siri"
                },
                ["smart_platform"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["airplay 2"] = "AirPlay 2",
                    ["google home"] = "Google Home",
                    ["sonos s2"] = "Sonos S2"
                },
                ["charging_port"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["usb c"] = "USB-C",
                    ["usb type c"] = "USB-C",
                    ["micro usb"] = "Micro-USB"
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
            "wireless" or "water_resistant" or "stereo_pairing" or "multiroom_support" => NormaliseRetailerBoolean(rawAttribute, definition),
            "battery_life_hours" or "width_mm" or "height_mm" or "depth_mm" => NormaliseDecimalMeasurement(rawAttribute, definition),
            "power_output_w" or "weight_g" or "release_year" => NormaliseIntegerMeasurement(rawAttribute, definition),
            "bluetooth_version" => NormaliseBluetoothVersion(rawAttribute, definition),
            "ip_rating" => NormaliseIpRating(rawAttribute, definition),
            "colour" => NormaliseTitleCaseString(rawAttribute, definition),
            _ => default!
        };

        return normalisedValue is not null;
    }

    private static NormalisedAttributeValue NormaliseRetailerBoolean(SourceAttributeValue rawAttribute, CanonicalAttributeDefinition definition)
    {
        var normalisedValue = rawAttribute.Value?.Trim().ToLowerInvariant();
        var parsedValue = normalisedValue switch
        {
            "yes" or "true" or "1" or "supported" or "included" or "present" or "available" => true,
            "no" or "false" or "0" or "unsupported" or "not supported" or "not included" or "absent" => false,
            _ => (bool?)null
        };

        return parsedValue is not null
            ? new NormalisedAttributeValue
            {
                AttributeKey = definition.Key,
                Value = parsedValue.Value,
                ValueType = definition.ValueType,
                Unit = definition.Unit,
                Confidence = 0.97m,
                SourceAttributeKey = rawAttribute.AttributeKey,
                OriginalValue = rawAttribute.Value,
                ParseNotes = "Parsed retailer boolean value."
            }
            : NormaliseBoolean(rawAttribute, definition);
    }

    private static NormalisedAttributeValue NormaliseBluetoothVersion(SourceAttributeValue rawAttribute, CanonicalAttributeDefinition definition)
    {
        var match = LooseNumberPattern.Match(rawAttribute.Value ?? string.Empty);
        return match.Success
            ? new NormalisedAttributeValue
            {
                AttributeKey = definition.Key,
                Value = $"Bluetooth {match.Groups["value"].Value.Replace(',', '.')}",
                ValueType = definition.ValueType,
                Unit = definition.Unit,
                Confidence = 0.94m,
                SourceAttributeKey = rawAttribute.AttributeKey,
                OriginalValue = rawAttribute.Value,
                ParseNotes = "Normalised Bluetooth version label."
            }
            : PreserveString(rawAttribute, definition);
    }

    private static NormalisedAttributeValue NormaliseIpRating(SourceAttributeValue rawAttribute, CanonicalAttributeDefinition definition)
    {
        var candidate = rawAttribute.Value?.Trim() ?? string.Empty;
        var compact = candidate.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);
        var match = IpRatingPattern.Match(compact);
        if (!match.Success)
        {
            return PreserveString(rawAttribute, definition);
        }

        return new NormalisedAttributeValue
        {
            AttributeKey = definition.Key,
            Value = $"IP{match.Groups["rating"].Value.ToUpperInvariant()}",
            ValueType = definition.ValueType,
            Unit = definition.Unit,
            Confidence = 0.94m,
            SourceAttributeKey = rawAttribute.AttributeKey,
            OriginalValue = rawAttribute.Value,
            ParseNotes = "Normalised ingress protection rating."
        };
    }

    private static NormalisedAttributeValue NormaliseTitleCaseString(SourceAttributeValue rawAttribute, CanonicalAttributeDefinition definition)
    {
        var value = ToTitleCase(rawAttribute.Value);
        return string.IsNullOrWhiteSpace(value)
            ? PreserveString(rawAttribute, definition)
            : new NormalisedAttributeValue
            {
                AttributeKey = definition.Key,
                Value = value,
                ValueType = definition.ValueType,
                Unit = definition.Unit,
                Confidence = 0.88m,
                SourceAttributeKey = rawAttribute.AttributeKey,
                OriginalValue = rawAttribute.Value,
                ParseNotes = "Normalised retailer casing and separators."
            };
    }

    private static string ToTitleCase(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        var sanitised = rawValue.Trim()
            .Replace("_", " ", StringComparison.Ordinal)
            .Replace("-", " ", StringComparison.Ordinal);

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(sanitised.ToLowerInvariant());
    }
}