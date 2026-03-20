using ProductNormaliser.Core.Normalisation;

namespace ProductNormaliser.Tests;

public sealed class AttributeAliasDictionaryTests
{
    [TestCase("Screen Size", "screen_size_inch")]
    [TestCase("ScreenSize", "screen_size_inch")]
    [TestCase("Display Size", "screen_size_inch")]
    [TestCase("HDMI Ports", "hdmi_port_count")]
    [TestCase("Number of HDMI Ports", "hdmi_port_count")]
    [TestCase("Smart TV", "smart_tv")]
    [TestCase("Refresh Rate", "refresh_rate_hz")]
    [TestCase("Refresh-Rate!!!", "refresh_rate_hz")]
    public void Resolve_ReturnsExpectedCanonicalKey(string rawAttributeName, string expectedCanonicalKey)
    {
        var sut = new AttributeAliasDictionary();

        var result = sut.Resolve(rawAttributeName);

        Assert.That(result, Is.EqualTo(expectedCanonicalKey));
    }
}