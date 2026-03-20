using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Normalisation;

namespace ProductNormaliser.Core.Merging;

public sealed class ConflictDetector(
    MeasurementParser? measurementParser = null,
    UnitConversionService? unitConversionService = null,
    ValueMappingRegistry? valueMappingRegistry = null) : IConflictDetector
{
    private readonly MeasurementParser measurementParser = measurementParser ?? new MeasurementParser();
    private readonly UnitConversionService unitConversionService = unitConversionService ?? new UnitConversionService();
    private readonly ValueMappingRegistry valueMappingRegistry = valueMappingRegistry ?? new ValueMappingRegistry();

    public List<MergeConflict> Detect(CanonicalProduct product)
    {
        ArgumentNullException.ThrowIfNull(product);

        var conflicts = new List<MergeConflict>();

        foreach (var attribute in product.Attributes.Values)
        {
            if (attribute.Evidence.Count < 2 && !attribute.HasConflict)
            {
                continue;
            }

            var reason = DetectConflictReason(attribute);
            if (reason is null)
            {
                continue;
            }

            conflicts.Add(new MergeConflict
            {
                Id = $"{product.Id}:{attribute.AttributeKey}",
                CanonicalProductId = product.Id,
                AttributeKey = attribute.AttributeKey,
                ExistingValue = attribute.Value,
                IncomingValue = GetFirstContradictoryEvidenceValue(attribute),
                Reason = reason,
                Severity = attribute.HasConflict ? 0.90m : 0.75m,
                Status = "open",
                SuggestedValue = attribute.Value,
                SuggestedSourceName = attribute.WinningSourceName ?? GetMostTrustedSource(attribute),
                SuggestedConfidence = Math.Max(attribute.MergeWeight, attribute.Confidence),
                HighestConfidenceValue = GetHighestConfidenceValue(attribute),
                CreatedUtc = product.UpdatedUtc == default ? DateTime.UtcNow : product.UpdatedUtc
            });
        }

        return conflicts;
    }

    private string? DetectConflictReason(CanonicalAttributeValue attribute)
    {
        if (IsNumericValueType(attribute.ValueType) && HasMaterialNumericDifference(attribute))
        {
            return "Materially different numeric values detected across sources.";
        }

        if ((string.Equals(attribute.ValueType, "string", StringComparison.OrdinalIgnoreCase)
            || string.Equals(attribute.ValueType, "boolean", StringComparison.OrdinalIgnoreCase))
            && HasContradictoryDiscreteValues(attribute))
        {
            return "Contradictory categorical values detected across sources.";
        }

        return attribute.HasConflict
            ? "Source values disagree materially."
            : null;
    }

    private bool HasMaterialNumericDifference(CanonicalAttributeValue attribute)
    {
        var numericValues = new List<decimal>();

        foreach (var evidence in attribute.Evidence)
        {
            var parsedValue = TryParseEvidenceAsNumeric(attribute, evidence.RawValue);
            if (parsedValue is not null)
            {
                numericValues.Add(parsedValue.Value);
            }
        }

        if (numericValues.Count < 2)
        {
            return false;
        }

        var minValue = numericValues.Min();
        var maxValue = numericValues.Max();
        var tolerance = Math.Max(0.5m, Math.Abs(maxValue) * 0.03m);

        return maxValue - minValue > tolerance;
    }

    private bool HasContradictoryDiscreteValues(CanonicalAttributeValue attribute)
    {
        var values = attribute.Evidence
            .Select(evidence => NormaliseEvidenceValue(attribute.AttributeKey, evidence.RawValue))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return values.Length > 1;
    }

    private decimal? TryParseEvidenceAsNumeric(CanonicalAttributeValue attribute, string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var parseResult = measurementParser.Parse(rawValue);
        if (!parseResult.Success || parseResult.NumericValue is null)
        {
            return decimal.TryParse(rawValue.Trim(), out var numericValue) ? numericValue : null;
        }

        if (!string.IsNullOrWhiteSpace(attribute.Unit)
            && !string.IsNullOrWhiteSpace(parseResult.Unit)
            && !string.Equals(attribute.Unit, parseResult.Unit, StringComparison.OrdinalIgnoreCase))
        {
            return unitConversionService.TryConvert(parseResult.NumericValue.Value, parseResult.Unit, attribute.Unit, out var convertedValue)
                ? convertedValue
                : null;
        }

        return parseResult.NumericValue.Value;
    }

    private string? NormaliseEvidenceValue(string attributeKey, string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        if (valueMappingRegistry.TryMap(attributeKey, rawValue, out var mappedValue))
        {
            return mappedValue;
        }

        return rawValue.Trim().ToLowerInvariant();
    }

    private static object? GetFirstContradictoryEvidenceValue(CanonicalAttributeValue attribute)
    {
        return attribute.Evidence
            .Select(evidence => evidence.RawValue)
            .FirstOrDefault(rawValue => !string.Equals(rawValue?.Trim(), attribute.Value?.ToString()?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private object? GetHighestConfidenceValue(CanonicalAttributeValue attribute)
    {
        var candidate = attribute.Evidence
            .Where(evidence => !string.IsNullOrWhiteSpace(evidence.RawValue))
            .OrderByDescending(evidence => evidence.Confidence)
            .ThenByDescending(evidence => evidence.ObservedUtc)
            .FirstOrDefault();

        if (candidate?.RawValue is null)
        {
            return attribute.Value;
        }

        if (IsNumericValueType(attribute.ValueType))
        {
            var numericValue = TryParseEvidenceAsNumeric(attribute, candidate.RawValue);
            return numericValue is not null ? numericValue.Value : candidate.RawValue;
        }

        return NormaliseEvidenceValue(attribute.AttributeKey, candidate.RawValue) ?? candidate.RawValue;
    }

    private static string? GetMostTrustedSource(CanonicalAttributeValue attribute)
    {
        return attribute.Evidence
            .OrderByDescending(evidence => evidence.Confidence)
            .ThenByDescending(evidence => evidence.ObservedUtc)
            .Select(evidence => evidence.SourceName)
            .FirstOrDefault();
    }

    private static bool IsNumericValueType(string valueType)
    {
        return string.Equals(valueType, "decimal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(valueType, "integer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(valueType, "number", StringComparison.OrdinalIgnoreCase);
    }
}