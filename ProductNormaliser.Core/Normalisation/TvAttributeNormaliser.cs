using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Core.Normalisation;

public sealed class TvAttributeNormaliser : IAttributeNormaliser
{
    private readonly AttributeNameNormaliser attributeNameNormaliser;
    private readonly AttributeAliasDictionary attributeAliasDictionary;
    private readonly MeasurementParser measurementParser;
    private readonly UnitConversionService unitConversionService;
    private readonly ValueMappingRegistry valueMappingRegistry;
    private readonly IUnmappedAttributeRecorder unmappedAttributeRecorder;
    private readonly IReadOnlyDictionary<string, CanonicalAttributeDefinition> schemaAttributes;

    public TvAttributeNormaliser(
        AttributeNameNormaliser? attributeNameNormaliser = null,
        AttributeAliasDictionary? attributeAliasDictionary = null,
        MeasurementParser? measurementParser = null,
        UnitConversionService? unitConversionService = null,
        ValueMappingRegistry? valueMappingRegistry = null,
        IUnmappedAttributeRecorder? unmappedAttributeRecorder = null)
    {
        this.attributeNameNormaliser = attributeNameNormaliser ?? new AttributeNameNormaliser();
        this.attributeAliasDictionary = attributeAliasDictionary ?? new AttributeAliasDictionary(this.attributeNameNormaliser);
        this.measurementParser = measurementParser ?? new MeasurementParser();
        this.unitConversionService = unitConversionService ?? new UnitConversionService(this.measurementParser);
        this.valueMappingRegistry = valueMappingRegistry ?? new ValueMappingRegistry();
        this.unmappedAttributeRecorder = unmappedAttributeRecorder ?? NullUnmappedAttributeRecorder.Instance;
        schemaAttributes = new TvCategorySchemaProvider()
            .GetSchema()
            .Attributes
            .ToDictionary(attribute => attribute.Key, StringComparer.Ordinal);
    }

    public Dictionary<string, NormalisedAttributeValue> Normalise(
        string categoryKey,
        Dictionary<string, SourceAttributeValue> rawAttributes)
    {
        if (!string.Equals(categoryKey, TvCategorySchemaProvider.CategoryKey, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var results = new Dictionary<string, NormalisedAttributeValue>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawAttribute in rawAttributes.Values)
        {
            var canonicalKey = ResolveCanonicalKey(rawAttribute.AttributeKey);
            results[canonicalKey] = NormaliseSingle(rawAttribute, canonicalKey);
        }

        return results;
    }

    private string ResolveCanonicalKey(string rawAttributeKey)
    {
        var aliasMatch = attributeAliasDictionary.Resolve(rawAttributeKey);
        if (!string.IsNullOrWhiteSpace(aliasMatch))
        {
            return aliasMatch;
        }

        return attributeNameNormaliser.Normalise(rawAttributeKey).Replace(' ', '_');
    }

    private NormalisedAttributeValue NormaliseSingle(SourceAttributeValue rawAttribute, string canonicalKey)
    {
        if (!schemaAttributes.TryGetValue(canonicalKey, out var definition))
        {
            unmappedAttributeRecorder.Record(TvCategorySchemaProvider.CategoryKey, canonicalKey, rawAttribute);

            return new NormalisedAttributeValue
            {
                AttributeKey = canonicalKey,
                Value = rawAttribute.Value?.Trim(),
                ValueType = rawAttribute.ValueType,
                Unit = rawAttribute.Unit,
                Confidence = 0.35m,
                SourceAttributeKey = rawAttribute.AttributeKey,
                OriginalValue = rawAttribute.Value,
                ParseNotes = "No schema definition found; preserved raw value."
            };
        }

        if (valueMappingRegistry.TryMap(canonicalKey, rawAttribute.Value, out var mappedValue))
        {
            return new NormalisedAttributeValue
            {
                AttributeKey = canonicalKey,
                Value = mappedValue,
                ValueType = definition.ValueType,
                Unit = definition.Unit,
                Confidence = 0.98m,
                SourceAttributeKey = rawAttribute.AttributeKey,
                OriginalValue = rawAttribute.Value,
                ParseNotes = "Mapped known source value to canonical value."
            };
        }

        return canonicalKey switch
        {
            "screen_size_inch" => NormaliseScreenSize(rawAttribute, definition),
            "refresh_rate_hz" => NormaliseIntegerMeasurement(rawAttribute, definition),
            "hdmi_port_count" => NormaliseInteger(rawAttribute, definition),
            "smart_tv" => NormaliseBoolean(rawAttribute, definition),
            "width_mm" or "height_mm" or "depth_mm" or "vesa_mount_width_mm" or "vesa_mount_height_mm" => NormaliseDecimalMeasurement(rawAttribute, definition),
            _ => new NormalisedAttributeValue
            {
                AttributeKey = canonicalKey,
                Value = rawAttribute.Value?.Trim(),
                ValueType = definition.ValueType,
                Unit = definition.Unit,
                Confidence = 0.80m,
                SourceAttributeKey = rawAttribute.AttributeKey,
                OriginalValue = rawAttribute.Value,
                ParseNotes = "Preserved string value without transformation."
            }
        };
    }

    private NormalisedAttributeValue NormaliseScreenSize(SourceAttributeValue rawAttribute, CanonicalAttributeDefinition definition)
    {
        var measurement = measurementParser.Parse(rawAttribute.Value);
        if (!measurement.Success || measurement.NumericValue is null)
        {
            return CreateMalformedValue(rawAttribute, definition, "Unable to parse screen size measurement.");
        }

        var numericValue = measurement.NumericValue.Value;
        var unit = measurement.Unit ?? definition.Unit ?? "inch";
        if (!string.Equals(unit, "inch", StringComparison.Ordinal))
        {
            if (!unitConversionService.TryConvert(numericValue, unit, "inch", out numericValue))
            {
                return CreateMalformedValue(rawAttribute, definition, "Unable to convert screen size measurement to inches.");
            }
        }

        var roundedValue = decimal.Round(numericValue, 0, MidpointRounding.AwayFromZero);
        return new NormalisedAttributeValue
        {
            AttributeKey = definition.Key,
            Value = roundedValue,
            ValueType = definition.ValueType,
            Unit = definition.Unit,
            Confidence = 0.96m,
            SourceAttributeKey = rawAttribute.AttributeKey,
            OriginalValue = rawAttribute.Value,
            ParseNotes = $"{measurement.Notes} Normalised to marketing inches."
        };
    }

    private NormalisedAttributeValue NormaliseIntegerMeasurement(SourceAttributeValue rawAttribute, CanonicalAttributeDefinition definition)
    {
        var measurement = measurementParser.Parse(rawAttribute.Value);
        if (!measurement.Success || measurement.NumericValue is null)
        {
            return CreateMalformedValue(rawAttribute, definition, "Unable to parse measurement.");
        }

        var numericValue = measurement.NumericValue.Value;
        var sourceUnit = measurement.Unit ?? definition.Unit;
        if (!string.IsNullOrWhiteSpace(definition.Unit)
            && !string.IsNullOrWhiteSpace(sourceUnit)
            && !string.Equals(sourceUnit, definition.Unit, StringComparison.OrdinalIgnoreCase))
        {
            if (!unitConversionService.TryConvert(numericValue, sourceUnit, definition.Unit, out numericValue))
            {
                return CreateMalformedValue(rawAttribute, definition, $"Unable to convert measurement to {definition.Unit}.");
            }
        }

        return new NormalisedAttributeValue
        {
            AttributeKey = definition.Key,
            Value = decimal.ToInt32(decimal.Round(numericValue, 0, MidpointRounding.AwayFromZero)),
            ValueType = definition.ValueType,
            Unit = definition.Unit,
            Confidence = 0.95m,
            SourceAttributeKey = rawAttribute.AttributeKey,
            OriginalValue = rawAttribute.Value,
            ParseNotes = measurement.Notes
        };
    }

    private NormalisedAttributeValue NormaliseDecimalMeasurement(SourceAttributeValue rawAttribute, CanonicalAttributeDefinition definition)
    {
        var measurement = measurementParser.Parse(rawAttribute.Value);
        if (!measurement.Success || measurement.NumericValue is null)
        {
            return CreateMalformedValue(rawAttribute, definition, "Unable to parse measurement.");
        }

        var numericValue = measurement.NumericValue.Value;
        var sourceUnit = measurement.Unit ?? definition.Unit;
        if (!string.IsNullOrWhiteSpace(definition.Unit)
            && !string.IsNullOrWhiteSpace(sourceUnit)
            && !string.Equals(sourceUnit, definition.Unit, StringComparison.OrdinalIgnoreCase))
        {
            if (!unitConversionService.TryConvert(numericValue, sourceUnit, definition.Unit, out numericValue))
            {
                return CreateMalformedValue(rawAttribute, definition, $"Unable to convert measurement to {definition.Unit}.");
            }
        }

        return new NormalisedAttributeValue
        {
            AttributeKey = definition.Key,
            Value = numericValue,
            ValueType = definition.ValueType,
            Unit = definition.Unit,
            Confidence = 0.94m,
            SourceAttributeKey = rawAttribute.AttributeKey,
            OriginalValue = rawAttribute.Value,
            ParseNotes = measurement.Notes
        };
    }

    private static NormalisedAttributeValue NormaliseInteger(SourceAttributeValue rawAttribute, CanonicalAttributeDefinition definition)
    {
        if (int.TryParse(rawAttribute.Value?.Trim(), out var numericValue))
        {
            return new NormalisedAttributeValue
            {
                AttributeKey = definition.Key,
                Value = numericValue,
                ValueType = definition.ValueType,
                Unit = definition.Unit,
                Confidence = 0.97m,
                SourceAttributeKey = rawAttribute.AttributeKey,
                OriginalValue = rawAttribute.Value,
                ParseNotes = "Parsed integer value."
            };
        }

        return new NormalisedAttributeValue
        {
            AttributeKey = definition.Key,
            Value = rawAttribute.Value?.Trim(),
            ValueType = definition.ValueType,
            Unit = definition.Unit,
            Confidence = 0.30m,
            SourceAttributeKey = rawAttribute.AttributeKey,
            OriginalValue = rawAttribute.Value,
            ParseNotes = "Unable to parse integer value; preserved raw value."
        };
    }

    private static NormalisedAttributeValue NormaliseBoolean(SourceAttributeValue rawAttribute, CanonicalAttributeDefinition definition)
    {
        var normalisedValue = rawAttribute.Value?.Trim().ToLowerInvariant();
        var parsedValue = normalisedValue switch
        {
            "yes" or "true" or "1" => true,
            "no" or "false" or "0" => false,
            _ => (bool?)null
        };

        if (parsedValue is not null)
        {
            return new NormalisedAttributeValue
            {
                AttributeKey = definition.Key,
                Value = parsedValue.Value,
                ValueType = definition.ValueType,
                Unit = definition.Unit,
                Confidence = 0.97m,
                SourceAttributeKey = rawAttribute.AttributeKey,
                OriginalValue = rawAttribute.Value,
                ParseNotes = "Parsed boolean value."
            };
        }

        return new NormalisedAttributeValue
        {
            AttributeKey = definition.Key,
            Value = rawAttribute.Value?.Trim(),
            ValueType = definition.ValueType,
            Unit = definition.Unit,
            Confidence = 0.30m,
            SourceAttributeKey = rawAttribute.AttributeKey,
            OriginalValue = rawAttribute.Value,
            ParseNotes = "Unable to parse boolean value; preserved raw value."
        };
    }

    private static NormalisedAttributeValue CreateMalformedValue(SourceAttributeValue rawAttribute, CanonicalAttributeDefinition definition, string note)
    {
        return new NormalisedAttributeValue
        {
            AttributeKey = definition.Key,
            Value = rawAttribute.Value?.Trim(),
            ValueType = definition.ValueType,
            Unit = definition.Unit,
            Confidence = 0.25m,
            SourceAttributeKey = rawAttribute.AttributeKey,
            OriginalValue = rawAttribute.Value,
            ParseNotes = note
        };
    }
}