using System.Globalization;
using System.Text.RegularExpressions;
using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Core.Normalisation;

public sealed class SmartphoneAttributeNormaliser : CategoryAttributeNormaliserBase
{
    private static readonly Regex LooseNumberPattern = new(@"(?<value>\d+(?:[\.,]\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex IpRatingPattern = new(@"\bip\s*[- ]?\s*(?<rating>[a-z0-9]{2,4})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public SmartphoneAttributeNormaliser(
        AttributeNameNormaliser? attributeNameNormaliser = null,
        MeasurementParser? measurementParser = null,
        UnitConversionService? unitConversionService = null,
        IUnmappedAttributeRecorder? unmappedAttributeRecorder = null)
        : base(
            SmartphoneCategorySchemaProvider.CategoryKey,
            new SmartphoneCategorySchemaProvider().GetSchema(),
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
                "storage_capacity_gb",
                "ram_gb",
                "carrier_lock_status"
            ],
            completenessAttributeKeys:
            [
                "brand",
                "model_number",
                "model_family",
                "storage_capacity_gb",
                "ram_gb",
                "operating_system",
                "cellular_generation",
                "screen_size_inch",
                "native_resolution",
                "display_technology",
                "refresh_rate_hz",
                "chipset_model",
                "rear_camera_primary_mp",
                "battery_capacity_mah",
                "charging_port",
                "nfc",
                "ip_rating"
            ],
            aliases: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["screen size"] = "screen_size_inch",
                ["screensize"] = "screen_size_inch",
                ["display size"] = "screen_size_inch",
                ["screen diagonal"] = "screen_size_inch",
                ["storage"] = "storage_capacity_gb",
                ["storage capacity"] = "storage_capacity_gb",
                ["internal storage"] = "storage_capacity_gb",
                ["built in storage"] = "storage_capacity_gb",
                ["built in memory"] = "storage_capacity_gb",
                ["internal memory"] = "storage_capacity_gb",
                ["rom"] = "storage_capacity_gb",
                ["memory"] = "ram_gb",
                ["ram"] = "ram_gb",
                ["system memory"] = "ram_gb",
                ["ram memory"] = "ram_gb",
                ["installed ram"] = "ram_gb",
                ["os"] = "operating_system",
                ["operating system"] = "operating_system",
                ["network"] = "cellular_generation",
                ["mobile network"] = "cellular_generation",
                ["network type"] = "cellular_generation",
                ["mobile data"] = "cellular_generation",
                ["sim type"] = "sim_form_factor",
                ["sim slot"] = "sim_form_factor",
                ["esim"] = "esim_support",
                ["e sim"] = "esim_support",
                ["esim support"] = "esim_support",
                ["carrier lock"] = "carrier_lock_status",
                ["network lock"] = "carrier_lock_status",
                ["lock status"] = "carrier_lock_status",
                ["display type"] = "display_technology",
                ["panel type"] = "display_technology",
                ["refresh rate"] = "refresh_rate_hz",
                ["processor"] = "chipset_model",
                ["chipset"] = "chipset_model",
                ["soc"] = "chipset_model",
                ["main camera"] = "rear_camera_primary_mp",
                ["primary camera"] = "rear_camera_primary_mp",
                ["rear camera"] = "rear_camera_primary_mp",
                ["front camera"] = "front_camera_mp",
                ["selfie camera"] = "front_camera_mp",
                ["battery"] = "battery_capacity_mah",
                ["battery capacity"] = "battery_capacity_mah",
                ["charge port"] = "charging_port",
                ["charging interface"] = "charging_port",
                ["connector type"] = "charging_port",
                ["wireless charging support"] = "wireless_charging",
                ["nfc support"] = "nfc",
                ["water resistance"] = "ip_rating",
                ["ingress protection"] = "ip_rating",
                ["color"] = "colour",
                ["dual sim"] = "dual_sim",
                ["thickness"] = "depth_mm",
                ["weight"] = "weight_g"
            },
            valueMappings: new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
            {
                ["operating_system"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["android"] = "Android",
                    ["android 14"] = "Android",
                    ["android 15"] = "Android",
                    ["android os"] = "Android",
                    ["ios"] = "iOS",
                    ["ios 17"] = "iOS",
                    ["ios 18"] = "iOS",
                    ["apple ios"] = "iOS"
                },
                ["cellular_generation"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["5g"] = "5G",
                    ["5g nr"] = "5G",
                    ["nr 5g"] = "5G",
                    ["5g ready"] = "5G",
                    ["4g"] = "4G",
                    ["4g lte"] = "4G",
                    ["lte"] = "4G"
                },
                ["sim_form_factor"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["nano sim"] = "Nano-SIM",
                    ["dual nano sim"] = "Dual Nano-SIM",
                    ["esim"] = "eSIM",
                    ["nano sim esim"] = "Nano-SIM + eSIM",
                    ["nano sim plus esim"] = "Nano-SIM + eSIM",
                    ["dual sim nano sim esim"] = "Dual SIM (Nano-SIM + eSIM)"
                },
                ["carrier_lock_status"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["sim free"] = "Unlocked",
                    ["simfree"] = "Unlocked",
                    ["sim free unlocked"] = "Unlocked",
                    ["unlocked"] = "Unlocked",
                    ["carrier locked"] = "Carrier Locked",
                    ["network locked"] = "Carrier Locked",
                    ["locked"] = "Carrier Locked"
                },
                ["display_technology"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["amoled"] = "AMOLED",
                    ["super amoled"] = "Super AMOLED",
                    ["dynamic amoled 2x"] = "Dynamic AMOLED 2X",
                    ["dynamic amoled"] = "Dynamic AMOLED",
                    ["oled"] = "OLED",
                    ["ltpo oled"] = "LTPO OLED",
                    ["ltpo amoled"] = "LTPO AMOLED",
                    ["lcd"] = "LCD"
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
            "screen_size_inch" => NormaliseScreenSize(rawAttribute, definition),
            "storage_capacity_gb" or "ram_gb" or "refresh_rate_hz" or "release_year" or "weight_g" => NormaliseIntegerMeasurement(rawAttribute, definition),
            "battery_capacity_mah" => NormaliseLooseIntegerMeasurement(rawAttribute, definition),
            "rear_camera_primary_mp" or "front_camera_mp" => NormaliseLooseDecimalMeasurement(rawAttribute, definition),
            "width_mm" or "height_mm" or "depth_mm" => NormaliseLooseDecimalMeasurement(rawAttribute, definition),
            "dual_sim" or "esim_support" or "wireless_charging" or "nfc" => NormaliseRetailerBoolean(rawAttribute, definition),
            "colour" => NormaliseTitleCaseString(rawAttribute, definition),
            "ip_rating" => NormaliseIpRating(rawAttribute, definition),
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
            "yes" or "true" or "1" or "supported" or "included" or "present" or "available" or "built in" or "built-in" => true,
            "no" or "false" or "0" or "unsupported" or "not supported" or "not included" or "absent" or "none" => false,
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