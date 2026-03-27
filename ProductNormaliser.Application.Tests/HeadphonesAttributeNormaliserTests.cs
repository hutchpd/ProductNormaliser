using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Normalisation;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.Normalisation)]
public sealed class HeadphonesAttributeNormaliserTests
{
    [Test]
    public void HeadphonesNormaliser_ExposesIdentityAndCompletenessKeysForMatureSchema()
    {
        var sut = new HeadphonesAttributeNormaliser();

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
                "form_factor",
                "connection_type"
            }));
            Assert.That(sut.CompletenessAttributeKeys, Is.SupersetOf(new[]
            {
                "bluetooth_version",
                "case_battery_life_hours",
                "charging_port",
                "ip_rating"
            }));
        });
    }

    [Test]
    public void Normalise_MapsHeadphoneRetailerLabelsToStableCanonicalOutput()
    {
        var sut = new HeadphonesAttributeNormaliser();

        var result = sut.Normalise("headphones", new Dictionary<string, SourceAttributeValue>
        {
            ["Headphone Style"] = new() { AttributeKey = "Headphone Style", Value = "true wireless earbuds", ValueType = "string" },
            ["Wireless Connectivity"] = new() { AttributeKey = "Wireless Connectivity", Value = "bluetooth wireless", ValueType = "string" },
            ["Bluetooth Ver."] = new() { AttributeKey = "Bluetooth Ver.", Value = "v5.3", ValueType = "string" },
            ["Total Playtime"] = new() { AttributeKey = "Total Playtime", Value = "30 hrs", ValueType = "string" },
            ["Charging Connector"] = new() { AttributeKey = "Charging Connector", Value = "USB Type C", ValueType = "string" },
            ["Water Resistance Rating"] = new() { AttributeKey = "Water Resistance Rating", Value = "ipx4", ValueType = "string" },
            ["Color"] = new() { AttributeKey = "Color", Value = "midnight black", ValueType = "string" }
        });

        Assert.Multiple(() =>
        {
            Assert.That(result["form_factor"].Value, Is.EqualTo("In-Ear"));
            Assert.That(result["connection_type"].Value, Is.EqualTo("Bluetooth"));
            Assert.That(result["bluetooth_version"].Value, Is.EqualTo("Bluetooth 5.3"));
            Assert.That(result["case_battery_life_hours"].Value, Is.EqualTo(30m));
            Assert.That(result["charging_port"].Value, Is.EqualTo("USB-C"));
            Assert.That(result["ip_rating"].Value, Is.EqualTo("IPX4"));
            Assert.That(result["colour"].Value, Is.EqualTo("Midnight Black"));
        });
    }

    [Test]
    public void Normalise_HandlesNoiseCancellingAndImpedanceRetailerFormats()
    {
        var sut = new HeadphonesAttributeNormaliser();

        var result = sut.Normalise("headphones", new Dictionary<string, SourceAttributeValue>
        {
            ["ANC"] = new() { AttributeKey = "ANC", Value = "Supported", ValueType = "string" },
            ["Impedance"] = new() { AttributeKey = "Impedance", Value = "32 ohms", ValueType = "string" },
            ["Battery Life"] = new() { AttributeKey = "Battery Life", Value = "8 hr", ValueType = "string" }
        });

        Assert.Multiple(() =>
        {
            Assert.That(result["noise_cancelling"].Value, Is.EqualTo(true));
            Assert.That(result["impedance_ohm"].Value, Is.EqualTo(32));
            Assert.That(result["battery_life_hours"].Value, Is.EqualTo(8m));
        });
    }
}