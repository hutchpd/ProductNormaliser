using ProductNormaliser.Core.Normalisation;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.Normalisation)]
public sealed class MeasurementAndConversionTests
{
    [TestCase("55 in", true, 55, "inch")]
    [TestCase("55 inches", true, 55, "inch")]
    [TestCase("140 cm", true, 140, "cm")]
    [TestCase("100 Hz", true, 100, "hz")]
    [TestCase("55", true, 55, null)]
    [TestCase("not-a-number", false, null, null)]
    public void Parse_ReadsMeasurements(string rawValue, bool expectedSuccess, decimal? expectedValue, string? expectedUnit)
    {
        var sut = new MeasurementParser();

        var result = sut.Parse(rawValue);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.EqualTo(expectedSuccess));
            Assert.That(result.NumericValue, Is.EqualTo(expectedValue));
            Assert.That(result.Unit, Is.EqualTo(expectedUnit));
        });
    }

    [TestCase(140, "cm", "inch", 55.12)]
    [TestCase(55, "inch", "mm", 1397.00)]
    [TestCase(140, "cm", "mm", 1400.00)]
    public void TryConvert_ConvertsSupportedUnits(decimal numericValue, string fromUnit, string targetUnit, decimal expectedValue)
    {
        var sut = new UnitConversionService();

        var success = sut.TryConvert(numericValue, fromUnit, targetUnit, out var convertedValue);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.True);
            Assert.That(convertedValue, Is.EqualTo(expectedValue));
        });
    }
}