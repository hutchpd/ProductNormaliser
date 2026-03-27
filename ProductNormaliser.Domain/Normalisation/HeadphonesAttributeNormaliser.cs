using System.Globalization;
using System.Text.RegularExpressions;
using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Core.Normalisation;

public sealed class HeadphonesAttributeNormaliser : CategoryAttributeNormaliserBase
{
    private static readonly Regex LooseNumberPattern = new(@"(?<value>\d+(?:[\.,]\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex IpRatingPattern = new(@"\bip\s*[- ]?\s*(?<rating>[a-z0-9]{2,4})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public HeadphonesAttributeNormaliser(
        AttributeNameNormaliser? attributeNameNormaliser = null,
        MeasurementParser? measurementParser = null,
        UnitConversionService? unitConversionService = null,
        IUnmappedAttributeRecorder? unmappedAttributeRecorder = null)
        : base(
            HeadphonesCategorySchemaProvider.CategoryKey,
            new HeadphonesCategorySchemaProvider().GetSchema(),
            identityAttributeKeys:
            [
                "gtin",
                "brand",
                "model_number",
                "model_family",
                "variant_name",
                "manufacturer_part_number",
                "colour",
                "form_factor",
                "connection_type"
            ],
            completenessAttributeKeys:
            [
                "brand",
                "model_number",
                "model_family",
                "form_factor",
                "connection_type",
                "wireless",
                "bluetooth_version",
                "noise_cancelling",
                "battery_life_hours",
                "case_battery_life_hours",
                "charging_port",
                "driver_size_mm",
                "ip_rating"
            ],
            aliases: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["type"] = "form_factor",
                ["fit"] = "form_factor",
                ["headphone type"] = "form_factor",
                ["headphone style"] = "form_factor",
                ["earbud type"] = "form_factor",
                ["connectivity"] = "connection_type",
                ["connection"] = "connection_type",
                ["wireless connectivity"] = "connection_type",
                ["wireless"] = "wireless",
                ["bluetooth"] = "bluetooth_version",
                ["bluetooth ver"] = "bluetooth_version",
                ["bluetooth version"] = "bluetooth_version",
                ["bt version"] = "bluetooth_version",
                ["noise cancelling"] = "noise_cancelling",
                ["active noise cancellation"] = "noise_cancelling",
                ["anc"] = "noise_cancelling",
                ["battery life"] = "battery_life_hours",
                ["playtime"] = "battery_life_hours",
                ["case battery life"] = "case_battery_life_hours",
                ["charging case battery life"] = "case_battery_life_hours",
                ["total playtime"] = "case_battery_life_hours",
                ["charging port"] = "charging_port",
                ["charging connector"] = "charging_port",
                ["driver size"] = "driver_size_mm",
                ["impedance"] = "impedance_ohm",
                ["water resistance rating"] = "ip_rating",
                ["protection rating"] = "ip_rating",
                ["microphone"] = "microphone",
                ["weight"] = "weight_g",
                ["color"] = "colour"
            },
            valueMappings: new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
            {
                ["form_factor"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["over-ear"] = "Over-Ear",
                    ["on-ear"] = "On-Ear",
                    ["in-ear"] = "In-Ear",
                    ["earbuds"] = "In-Ear",
                    ["true wireless earbuds"] = "In-Ear",
                    ["true wireless"] = "In-Ear"
                },
                ["connection_type"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["bluetooth"] = "Bluetooth",
                    ["wireless bluetooth"] = "Bluetooth",
                    ["bluetooth wireless"] = "Bluetooth",
                    ["wired"] = "Wired",
                    ["usb-c"] = "USB-C",
                    ["usb type c"] = "USB-C",
                    ["3.5mm"] = "3.5 mm",
                    ["3.5 mm"] = "3.5 mm"
                },
                ["charging_port"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["usb c"] = "USB-C",
                    ["usb type c"] = "USB-C",
                    ["lightning"] = "Lightning",
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
            "wireless" or "noise_cancelling" or "microphone" or "multipoint_support" => NormaliseRetailerBoolean(rawAttribute, definition),
            "battery_life_hours" or "case_battery_life_hours" or "driver_size_mm" => NormaliseDecimalMeasurement(rawAttribute, definition),
            "weight_g" or "release_year" => NormaliseIntegerMeasurement(rawAttribute, definition),
            "impedance_ohm" => NormaliseLooseIntegerMeasurement(rawAttribute, definition),
            "bluetooth_version" => NormaliseBluetoothVersion(rawAttribute, definition),
            "ip_rating" => NormaliseIpRating(rawAttribute, definition),
            "colour" => NormaliseTitleCaseString(rawAttribute, definition),
            _ => default!
        };

        return normalisedValue is not null;
    }

    private NormalisedAttributeValue NormaliseLooseIntegerMeasurement(SourceAttributeValue rawAttribute, CanonicalAttributeDefinition definition)
    {
        var parsed = NormaliseIntegerMeasurement(rawAttribute, definition);
        if (parsed.Confidence > 0.30m)
        {
            return parsed;
        }

        return TryParseLooseDecimal(rawAttribute.Value, out var numericValue)
            ? new NormalisedAttributeValue
            {
                AttributeKey = definition.Key,
                Value = decimal.ToInt32(decimal.Round(numericValue, 0, MidpointRounding.AwayFromZero)),
                ValueType = definition.ValueType,
                Unit = definition.Unit,
                Confidence = 0.92m,
                SourceAttributeKey = rawAttribute.AttributeKey,
                OriginalValue = rawAttribute.Value,
                ParseNotes = "Parsed numeric value from compact retailer formatting."
            }
            : parsed;
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

    private static bool TryParseLooseDecimal(string? rawValue, out decimal numericValue)
    {
        numericValue = 0m;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var match = LooseNumberPattern.Match(rawValue);
        return match.Success
            && decimal.TryParse(match.Groups["value"].Value.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out numericValue);
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