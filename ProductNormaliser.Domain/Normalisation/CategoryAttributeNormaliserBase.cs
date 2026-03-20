using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Normalisation;

public abstract class CategoryAttributeNormaliserBase : ICategoryAttributeNormaliser, IAttributeNormaliser
{
    private readonly string supportedCategoryKey;
    private readonly AttributeNameNormaliser attributeNameNormaliser;
    private readonly AttributeAliasDictionary attributeAliasDictionary;
    private readonly MeasurementParser measurementParser;
    private readonly UnitConversionService unitConversionService;
    private readonly ValueMappingRegistry valueMappingRegistry;
    private readonly IUnmappedAttributeRecorder unmappedAttributeRecorder;
    private readonly IReadOnlyDictionary<string, CanonicalAttributeDefinition> schemaAttributes;

    protected CategoryAttributeNormaliserBase(
        string supportedCategoryKey,
        CategorySchema schema,
        IEnumerable<string> identityAttributeKeys,
        IEnumerable<string> completenessAttributeKeys,
        IReadOnlyDictionary<string, string>? aliases = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? valueMappings = null,
        AttributeNameNormaliser? attributeNameNormaliser = null,
        MeasurementParser? measurementParser = null,
        UnitConversionService? unitConversionService = null,
        IUnmappedAttributeRecorder? unmappedAttributeRecorder = null)
    {
        this.supportedCategoryKey = supportedCategoryKey;
        this.attributeNameNormaliser = attributeNameNormaliser ?? new AttributeNameNormaliser();
        this.attributeAliasDictionary = new AttributeAliasDictionary(this.attributeNameNormaliser, schema.Attributes, aliases);
        this.measurementParser = measurementParser ?? new MeasurementParser();
        this.unitConversionService = unitConversionService ?? new UnitConversionService(this.measurementParser);
        this.valueMappingRegistry = new ValueMappingRegistry(valueMappings);
        this.unmappedAttributeRecorder = unmappedAttributeRecorder ?? NullUnmappedAttributeRecorder.Instance;
        schemaAttributes = schema.Attributes.ToDictionary(attribute => attribute.Key, StringComparer.Ordinal);
        IdentityAttributeKeys = identityAttributeKeys.ToArray();
        CompletenessAttributeKeys = completenessAttributeKeys.ToArray();
    }

    public string SupportedCategoryKey => supportedCategoryKey;

    public IReadOnlyCollection<string> IdentityAttributeKeys { get; }

    public IReadOnlyCollection<string> CompletenessAttributeKeys { get; }

    public Dictionary<string, NormalisedAttributeValue> Normalise(string categoryKey, Dictionary<string, SourceAttributeValue> rawAttributes)
    {
        return string.Equals(categoryKey, supportedCategoryKey, StringComparison.OrdinalIgnoreCase)
            ? Normalise(rawAttributes)
            : [];
    }

    public Dictionary<string, NormalisedAttributeValue> Normalise(Dictionary<string, SourceAttributeValue> rawAttributes)
    {
        var results = new Dictionary<string, NormalisedAttributeValue>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawAttribute in rawAttributes.Values)
        {
            var canonicalKey = ResolveCanonicalKey(rawAttribute.AttributeKey);
            results[canonicalKey] = NormaliseSingle(rawAttribute, canonicalKey);
        }

        return results;
    }

    protected NormalisedAttributeValue NormaliseScreenSize(SourceAttributeValue rawAttribute, CanonicalAttributeDefinition definition)
    {
        var measurement = measurementParser.Parse(rawAttribute.Value);
        if (!measurement.Success || measurement.NumericValue is null)
        {
            return CreateMalformedValue(rawAttribute, definition, "Unable to parse screen size measurement.");
        }

        var numericValue = measurement.NumericValue.Value;
        var unit = measurement.Unit ?? definition.Unit ?? "inch";
        if (!string.Equals(unit, "inch", StringComparison.OrdinalIgnoreCase)
            && !unitConversionService.TryConvert(numericValue, unit, "inch", out numericValue))
        {
            return CreateMalformedValue(rawAttribute, definition, "Unable to convert screen size measurement to inches.");
        }

        return new NormalisedAttributeValue
        {
            AttributeKey = definition.Key,
            Value = decimal.Round(numericValue, 0, MidpointRounding.AwayFromZero),
            ValueType = definition.ValueType,
            Unit = definition.Unit,
            Confidence = 0.96m,
            SourceAttributeKey = rawAttribute.AttributeKey,
            OriginalValue = rawAttribute.Value,
            ParseNotes = $"{measurement.Notes} Normalised to marketing inches."
        };
    }

    protected NormalisedAttributeValue NormaliseIntegerMeasurement(SourceAttributeValue rawAttribute, CanonicalAttributeDefinition definition)
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
            && !string.Equals(sourceUnit, definition.Unit, StringComparison.OrdinalIgnoreCase)
            && !unitConversionService.TryConvert(numericValue, sourceUnit, definition.Unit, out numericValue))
        {
            return CreateMalformedValue(rawAttribute, definition, $"Unable to convert measurement to {definition.Unit}.");
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

    protected NormalisedAttributeValue NormaliseDecimalMeasurement(SourceAttributeValue rawAttribute, CanonicalAttributeDefinition definition)
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
            && !string.Equals(sourceUnit, definition.Unit, StringComparison.OrdinalIgnoreCase)
            && !unitConversionService.TryConvert(numericValue, sourceUnit, definition.Unit, out numericValue))
        {
            return CreateMalformedValue(rawAttribute, definition, $"Unable to convert measurement to {definition.Unit}.");
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

    protected static NormalisedAttributeValue NormaliseInteger(SourceAttributeValue rawAttribute, CanonicalAttributeDefinition definition)
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

    protected static NormalisedAttributeValue NormaliseBoolean(SourceAttributeValue rawAttribute, CanonicalAttributeDefinition definition)
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

    protected static NormalisedAttributeValue PreserveString(SourceAttributeValue rawAttribute, CanonicalAttributeDefinition definition, decimal confidence = 0.80m, string note = "Preserved string value without transformation.")
    {
        return new NormalisedAttributeValue
        {
            AttributeKey = definition.Key,
            Value = rawAttribute.Value?.Trim(),
            ValueType = definition.ValueType,
            Unit = definition.Unit,
            Confidence = confidence,
            SourceAttributeKey = rawAttribute.AttributeKey,
            OriginalValue = rawAttribute.Value,
            ParseNotes = note
        };
    }

    protected static NormalisedAttributeValue CreateMalformedValue(SourceAttributeValue rawAttribute, CanonicalAttributeDefinition definition, string note)
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

    protected abstract bool TryNormaliseKnownAttribute(SourceAttributeValue rawAttribute, CanonicalAttributeDefinition definition, out NormalisedAttributeValue normalisedValue);

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
            unmappedAttributeRecorder.Record(supportedCategoryKey, canonicalKey, rawAttribute);

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

        return TryNormaliseKnownAttribute(rawAttribute, definition, out var normalisedValue)
            ? normalisedValue
            : PreserveString(rawAttribute, definition);
    }
}