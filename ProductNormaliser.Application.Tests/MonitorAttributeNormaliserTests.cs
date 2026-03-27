using ProductNormaliser.Core.Normalisation;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.Normalisation)]
public sealed class MonitorAttributeNormaliserTests
{
    [Test]
    public void Normalise_MapsPanelResolutionAndPorts()
    {
        var sut = new MonitorAttributeNormaliser();

        var result = sut.Normalise("monitor", new Dictionary<string, SourceAttributeValue>
        {
            ["Panel Technology"] = new() { AttributeKey = "Panel Technology", Value = "ips", ValueType = "string" },
            ["Screen Resolution"] = new() { AttributeKey = "Screen Resolution", Value = "2560 x 1440", ValueType = "string" },
            ["DisplayPort Inputs"] = new() { AttributeKey = "DisplayPort Inputs", Value = "2", ValueType = "string" }
        });

        Assert.Multiple(() =>
        {
            Assert.That(result["panel_type"].Value, Is.EqualTo("IPS"));
            Assert.That(result["native_resolution"].Value, Is.EqualTo("1440p"));
            Assert.That(result["displayport_port_count"].Value, Is.EqualTo(2));
        });
    }

    [Test]
    public void Normalise_AppliesDisplayUnitRulesForRollout()
    {
        var sut = new MonitorAttributeNormaliser();

        var result = sut.Normalise("monitor", new Dictionary<string, SourceAttributeValue>
        {
            ["Display Size"] = new() { AttributeKey = "Display Size", Value = "68.6 cm", ValueType = "string" },
            ["VESA Horizontal"] = new() { AttributeKey = "VESA Horizontal", Value = "100 mm", ValueType = "string" }
        });

        Assert.Multiple(() =>
        {
            Assert.That(result["screen_size_inch"].Value, Is.EqualTo(27m));
            Assert.That(result["screen_size_inch"].Unit, Is.EqualTo("inch"));
            Assert.That(result["vesa_mount_width_mm"].Value, Is.EqualTo(100));
            Assert.That(result["vesa_mount_width_mm"].Unit, Is.EqualTo("mm"));
        });
    }

    [Test]
    public void MonitorNormaliser_ExposesIdentityAndCompletenessKeys()
    {
        var sut = new MonitorAttributeNormaliser();

        Assert.Multiple(() =>
        {
            Assert.That(sut.IdentityAttributeKeys, Is.SupersetOf(new[] { "gtin", "brand", "model_number", "screen_size_inch", "native_resolution", "panel_type" }));
            Assert.That(sut.CompletenessAttributeKeys, Is.SupersetOf(new[] { "refresh_rate_hz", "displayport_port_count" }));
        });
    }

    [Test]
    public void Normalise_MapsRepresentativeMonitorRetailerFixture()
    {
        var sut = new MonitorAttributeNormaliser();

        var result = sut.Normalise("monitor", new Dictionary<string, SourceAttributeValue>
        {
            ["Screen Diagonal"] = new() { AttributeKey = "Screen Diagonal", Value = "68.6 cm", ValueType = "string" },
            ["Panel Technology"] = new() { AttributeKey = "Panel Technology", Value = "oled", ValueType = "string" },
            ["Screen Resolution"] = new() { AttributeKey = "Screen Resolution", Value = "Ultra HD", ValueType = "string" },
            ["Refresh Rate"] = new() { AttributeKey = "Refresh Rate", Value = "165 Hz", ValueType = "string" },
            ["HDMI Ports"] = new() { AttributeKey = "HDMI Ports", Value = "2", ValueType = "string" },
            ["DisplayPort Inputs"] = new() { AttributeKey = "DisplayPort Inputs", Value = "1", ValueType = "string" },
            ["VESA Vertical"] = new() { AttributeKey = "VESA Vertical", Value = "100 mm", ValueType = "string" }
        });

        Assert.Multiple(() =>
        {
            Assert.That(result["screen_size_inch"].Value, Is.EqualTo(27m));
            Assert.That(result["panel_type"].Value, Is.EqualTo("OLED"));
            Assert.That(result["native_resolution"].Value, Is.EqualTo("4K"));
            Assert.That(result["refresh_rate_hz"].Value, Is.EqualTo(165));
            Assert.That(result["hdmi_port_count"].Value, Is.EqualTo(2));
            Assert.That(result["displayport_port_count"].Value, Is.EqualTo(1));
            Assert.That(result["vesa_mount_height_mm"].Value, Is.EqualTo(100));
        });
    }
}