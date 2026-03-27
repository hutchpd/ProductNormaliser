using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Normalisation;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.Normalisation)]
public sealed class SpeakersAttributeNormaliserTests
{
    [Test]
    public void SpeakersNormaliser_ExposesIdentityAndCompletenessKeysForMatureSchema()
    {
        var sut = new SpeakersAttributeNormaliser();

        Assert.Multiple(() =>
        {
            Assert.That(sut.IdentityAttributeKeys, Is.SupersetOf(new[]
            {
                "gtin",
                "brand",
                "model_number",
                "model_family",
                "variant_name",
                "manufacturer_part_number",
                "colour",
                "speaker_type",
                "connection_type"
            }));
            Assert.That(sut.CompletenessAttributeKeys, Is.SupersetOf(new[]
            {
                "bluetooth_version",
                "smart_platform",
                "stereo_pairing",
                "multiroom_support"
            }));
        });
    }

    [Test]
    public void Normalise_MapsSpeakerRetailerLabelsToStableCanonicalOutput()
    {
        var sut = new SpeakersAttributeNormaliser();

        var result = sut.Normalise("speakers", new Dictionary<string, SourceAttributeValue>
        {
            ["Speaker Category"] = new() { AttributeKey = "Speaker Category", Value = "portable bluetooth speaker", ValueType = "string" },
            ["Wireless Connectivity"] = new() { AttributeKey = "Wireless Connectivity", Value = "wi fi", ValueType = "string" },
            ["Bluetooth Ver."] = new() { AttributeKey = "Bluetooth Ver.", Value = "Bluetooth 5.3", ValueType = "string" },
            ["Voice Control"] = new() { AttributeKey = "Voice Control", Value = "amazon alexa", ValueType = "string" },
            ["Platform"] = new() { AttributeKey = "Platform", Value = "airplay 2", ValueType = "string" },
            ["Waterproof Rating"] = new() { AttributeKey = "Waterproof Rating", Value = "ip67", ValueType = "string" },
            ["Charging Interface"] = new() { AttributeKey = "Charging Interface", Value = "usb type c", ValueType = "string" },
            ["Color"] = new() { AttributeKey = "Color", Value = "ocean blue", ValueType = "string" }
        });

        Assert.Multiple(() =>
        {
            Assert.That(result["speaker_type"].Value, Is.EqualTo("Portable Bluetooth"));
            Assert.That(result["connection_type"].Value, Is.EqualTo("Wi-Fi"));
            Assert.That(result["bluetooth_version"].Value, Is.EqualTo("Bluetooth 5.3"));
            Assert.That(result["voice_assistant"].Value, Is.EqualTo("Alexa"));
            Assert.That(result["smart_platform"].Value, Is.EqualTo("AirPlay 2"));
            Assert.That(result["ip_rating"].Value, Is.EqualTo("IP67"));
            Assert.That(result["charging_port"].Value, Is.EqualTo("USB-C"));
            Assert.That(result["colour"].Value, Is.EqualTo("Ocean Blue"));
        });
    }

    [Test]
    public void Normalise_HandlesSpeakerBooleanAndUnitStyleNoise()
    {
        var sut = new SpeakersAttributeNormaliser();

        var result = sut.Normalise("speakers", new Dictionary<string, SourceAttributeValue>
        {
            ["Stereo Pair"] = new() { AttributeKey = "Stereo Pair", Value = "Included", ValueType = "string" },
            ["Multi-Room"] = new() { AttributeKey = "Multi-Room", Value = "Supported", ValueType = "string" },
            ["Power Output"] = new() { AttributeKey = "Power Output", Value = "120 W", ValueType = "string" },
            ["Battery Life"] = new() { AttributeKey = "Battery Life", Value = "24 hr", ValueType = "string" }
        });

        Assert.Multiple(() =>
        {
            Assert.That(result["stereo_pairing"].Value, Is.EqualTo(true));
            Assert.That(result["multiroom_support"].Value, Is.EqualTo(true));
            Assert.That(result["power_output_w"].Value, Is.EqualTo(120));
            Assert.That(result["battery_life_hours"].Value, Is.EqualTo(24m));
        });
    }
}