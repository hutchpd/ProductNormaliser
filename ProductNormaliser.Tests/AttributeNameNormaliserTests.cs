using ProductNormaliser.Core.Normalisation;

namespace ProductNormaliser.Tests;

public sealed class AttributeNameNormaliserTests
{
    [TestCase(" Screen Size ", "screen size")]
    [TestCase("ScreenSize", "screensize")]
    [TestCase("Screen_Size", "screen size")]
    [TestCase("Number-of HDMI Ports", "number of hdmi ports")]
    [TestCase("  Number---of__HDMI / Ports!!  ", "number of hdmi ports")]
    [TestCase("Refresh-Rate!!!", "refresh rate")]
    public void Normalise_CleansAttributeNames(string input, string expected)
    {
        var sut = new AttributeNameNormaliser();

        var result = sut.Normalise(input);

        Assert.That(result, Is.EqualTo(expected));
    }
}