using System.Globalization;
using System.Text.RegularExpressions;
using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Core.Normalisation;

public sealed class TabletAttributeNormaliser : CategoryAttributeNormaliserBase
{
    private static readonly Regex LooseNumberPattern = new(@"(?<value>\d+(?:[\.,]\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public TabletAttributeNormaliser(
        AttributeNameNormaliser? attributeNameNormaliser = null,
        MeasurementParser? measurementParser = null,
        UnitConversionService? unitConversionService = null,
        IUnmappedAttributeRecorder? unmappedAttributeRecorder = null)
        : base(
            TabletCategorySchemaProvider.CategoryKey,
            new TabletCategorySchemaProvider().GetSchema(),
            identityAttributeKeys:
            [
                "gtin",
                "brand",
                "model_number",
                "model_family",
                "variant_name",
                "manufacturer_part_number",
                "regional_variant",
                "colour",
                "display_size_inch",
                "storage_capacity_gb",
                "ram_gb",
                "connectivity"
            ],
            completenessAttributeKeys:
            [
                "brand",
                "model_number",
                "model_family",
                "display_size_inch",
                "native_resolution",
                "display_technology",
                "refresh_rate_hz",
                "storage_capacity_gb",
                "ram_gb",
                "operating_system",
                "connectivity",
                "chipset_model",
                "battery_capacity_mah",
                "stylus_support",
                "keyboard_support"
            ],
            aliases: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["screen size"] = "display_size_inch",
                ["screensize"] = "display_size_inch",
                ["display size"] = "display_size_inch",
                ["screen diagonal"] = "display_size_inch",
                ["storage"] = "storage_capacity_gb",
                ["storage capacity"] = "storage_capacity_gb",
                ["internal storage"] = "storage_capacity_gb",
                ["internal memory"] = "storage_capacity_gb",
                ["rom"] = "storage_capacity_gb",
                ["memory"] = "ram_gb",
                ["ram"] = "ram_gb",
                ["system memory"] = "ram_gb",
                ["os"] = "operating_system",
                ["platform"] = "operating_system",
                ["operating system"] = "operating_system",
                ["connectivity"] = "connectivity",
                ["connectivity type"] = "connectivity",
                ["network"] = "connectivity",
                ["network type"] = "cellular_generation",
                ["mobile network"] = "cellular_generation",
                ["battery life"] = "battery_life_hours",
                ["battery capacity"] = "battery_capacity_mah",
                ["processor"] = "chipset_model",
                ["chipset"] = "chipset_model",
                ["refresh rate"] = "refresh_rate_hz",
                ["display technology"] = "display_technology",
                ["display type"] = "display_technology",
                ["charge port"] = "charging_port",
                ["charging interface"] = "charging_port",
                ["weight"] = "weight_g",
                ["stylus"] = "stylus_support",
                ["pen support"] = "stylus_support",
                ["keyboard"] = "keyboard_support",
                ["keyboard support"] = "keyboard_support",
                ["main camera"] = "rear_camera_primary_mp",
                ["rear camera"] = "rear_camera_primary_mp",
                ["front camera"] = "front_camera_mp",
                ["thickness"] = "depth_mm",
                ["color"] = "colour"
            },
            valueMappings: new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
            {
                ["operating_system"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ipados"] = "iPadOS",
                    ["ipad os"] = "iPadOS",
                    ["apple ipados"] = "iPadOS",
                    ["android"] = "Android",
                    ["android 14"] = "Android",
                    ["windows 11"] = "Windows 11",
                    ["windows 11 home"] = "Windows 11 Home",
                    ["windows 11 pro"] = "Windows 11 Pro",
                    ["fire os"] = "Fire OS"
                },
                ["connectivity"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["wifi"] = "Wi-Fi",
                    ["wi fi"] = "Wi-Fi",
                    ["wifi only"] = "Wi-Fi",
                    ["wi-fi only"] = "Wi-Fi",
                    ["wifi + cellular"] = "Wi-Fi + Cellular",
                    ["wi-fi + cellular"] = "Wi-Fi + Cellular",
                    ["wifi + 5g"] = "Wi-Fi + Cellular",
                    ["wi-fi + 5g"] = "Wi-Fi + Cellular",
                    ["cellular"] = "Wi-Fi + Cellular",
                    ["lte"] = "Wi-Fi + Cellular",
                    ["5g"] = "Wi-Fi + Cellular",
                    ["4g"] = "Wi-Fi + Cellular"
                },
                ["cellular_generation"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["5g"] = "5G",
                    ["5g nr"] = "5G",
                    ["4g"] = "4G",
                    ["lte"] = "4G"
                },
                ["display_technology"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["lcd"] = "LCD",
                    ["oled"] = "OLED",
                    ["mini led"] = "Mini LED"
                },
                ["charging_port"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["usb c"] = "USB-C",
                    ["usb type c"] = "USB-C",
                    ["type c"] = "USB-C",
                    ["lightning"] = "Lightning"
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
            "storage_capacity_gb" or "ram_gb" or "weight_g" or "refresh_rate_hz" or "release_year" => NormaliseIntegerMeasurement(rawAttribute, definition),
            "battery_life_hours" => NormaliseDecimalMeasurement(rawAttribute, definition),
            "width_mm" or "height_mm" or "depth_mm" or "rear_camera_primary_mp" or "front_camera_mp" => NormaliseLooseDecimalMeasurement(rawAttribute, definition),
            "battery_capacity_mah" => NormaliseLooseIntegerMeasurement(rawAttribute, definition),
            "stylus_support" or "keyboard_support" => NormaliseRetailerBoolean(rawAttribute, definition),
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

    private NormalisedAttributeValue NormaliseLooseDecimalMeasurement(SourceAttributeValue rawAttribute, CanonicalAttributeDefinition definition)
    {
        var parsed = NormaliseDecimalMeasurement(rawAttribute, definition);
        if (parsed.Confidence > 0.30m)
        {
            return parsed;
        }

        return TryParseLooseDecimal(rawAttribute.Value, out var numericValue)
            ? new NormalisedAttributeValue
            {
                AttributeKey = definition.Key,
                Value = numericValue,
                ValueType = definition.ValueType,
                Unit = definition.Unit,
                Confidence = 0.91m,
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