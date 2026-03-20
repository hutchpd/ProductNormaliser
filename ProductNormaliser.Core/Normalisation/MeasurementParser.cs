using System.Globalization;
using System.Text.RegularExpressions;

namespace ProductNormaliser.Core.Normalisation;

public sealed class MeasurementParser
{
    private static readonly Regex MeasurementPattern = new(
        @"(?<value>\d+(?:[\.,]\d+)?)\s*(?<unit>inches|inch|in|cm|mm|hz)?\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public MeasurementParseResult Parse(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return new MeasurementParseResult
            {
                Success = false,
                Notes = "No value supplied."
            };
        }

        var match = MeasurementPattern.Match(rawValue.Trim());
        if (!match.Success)
        {
            return new MeasurementParseResult
            {
                Success = false,
                Notes = "Unable to parse numeric measurement."
            };
        }

        var numericCandidate = match.Groups["value"].Value.Replace(',', '.');
        if (!decimal.TryParse(numericCandidate, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var numericValue))
        {
            return new MeasurementParseResult
            {
                Success = false,
                Notes = "Unable to parse numeric measurement."
            };
        }

        var unit = NormaliseUnit(match.Groups["unit"].Value);
        var notes = unit is null
            ? "Parsed numeric value with no recognised unit."
            : $"Parsed numeric value in {unit}.";

        return new MeasurementParseResult
        {
            Success = true,
            NumericValue = numericValue,
            Unit = unit,
            Notes = notes
        };
    }

    private static string? NormaliseUnit(string rawUnit)
    {
        return rawUnit.Trim().ToLowerInvariant() switch
        {
            "in" or "inch" or "inches" => "inch",
            "cm" => "cm",
            "mm" => "mm",
            "hz" => "hz",
            _ => null
        };
    }
}