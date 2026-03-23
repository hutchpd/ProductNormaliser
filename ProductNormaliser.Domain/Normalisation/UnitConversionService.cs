using ProductNormaliser.Core.Interfaces;

namespace ProductNormaliser.Core.Normalisation;

public sealed class UnitConversionService(MeasurementParser? measurementParser = null) : IUnitConverter
{
    private readonly MeasurementParser measurementParser = measurementParser ?? new MeasurementParser();

    public object? Convert(string targetUnit, string rawValue, string valueType)
    {
        var parseResult = measurementParser.Parse(rawValue);
        if (!parseResult.Success || parseResult.NumericValue is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(parseResult.Unit))
        {
            return parseResult.NumericValue.Value;
        }

        return TryConvert(parseResult.NumericValue.Value, parseResult.Unit, targetUnit, out var convertedValue)
            ? convertedValue
            : null;
    }

    public bool TryConvert(decimal numericValue, string fromUnit, string targetUnit, out decimal convertedValue)
    {
        var normalisedFromUnit = NormaliseUnit(fromUnit);
        var normalisedTargetUnit = NormaliseUnit(targetUnit);

        if (normalisedFromUnit == normalisedTargetUnit)
        {
            convertedValue = numericValue;
            return true;
        }

        convertedValue = (normalisedFromUnit, normalisedTargetUnit) switch
        {
            ("cm", "inch") => Math.Round(numericValue / 2.54m, 2, MidpointRounding.AwayFromZero),
            ("inch", "mm") => Math.Round(numericValue * 25.4m, 2, MidpointRounding.AwayFromZero),
            ("cm", "mm") => Math.Round(numericValue * 10m, 2, MidpointRounding.AwayFromZero),
            ("mm", "inch") => Math.Round(numericValue / 25.4m, 2, MidpointRounding.AwayFromZero),
            ("tb", "gb") => Math.Round(numericValue * 1024m, 2, MidpointRounding.AwayFromZero),
            ("g", "kg") => Math.Round(numericValue / 1000m, 3, MidpointRounding.AwayFromZero),
            _ => 0m
        };

        return (normalisedFromUnit, normalisedTargetUnit) is ("cm", "inch") or ("inch", "mm") or ("cm", "mm") or ("mm", "inch") or ("tb", "gb") or ("g", "kg");
    }

    private static string NormaliseUnit(string unit)
    {
        return unit.Trim().ToLowerInvariant() switch
        {
            "in" or "inch" or "inches" => "inch",
            _ => unit.Trim().ToLowerInvariant()
        };
    }
}