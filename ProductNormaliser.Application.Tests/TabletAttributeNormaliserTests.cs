using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Normalisation;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.Normalisation)]
public sealed class TabletAttributeNormaliserTests
{
    [Test]
    public void TabletNormaliser_ExposesIdentityAndCompletenessKeysForMatureSchema()
    {
        var sut = new TabletAttributeNormaliser();

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
                "regional_variant",
                "storage_capacity_gb",
                "ram_gb",
                "connectivity"
            }));
            Assert.That(sut.CompletenessAttributeKeys, Is.SupersetOf(new[]
            {
                "display_technology",
                "chipset_model",
                "battery_capacity_mah",
                "keyboard_support"
            }));
        });
    }

    [Test]
    public void Normalise_MapsTabletRetailerAliasesAndConnectivityFormats()
    {
        var sut = new TabletAttributeNormaliser();

        var result = sut.Normalise("tablet", new Dictionary<string, SourceAttributeValue>
        {
            ["Internal Storage"] = new() { AttributeKey = "Internal Storage", Value = "1 TB", ValueType = "string" },
            ["Operating System"] = new() { AttributeKey = "Operating System", Value = "iPad OS", ValueType = "string" },
            ["Connectivity"] = new() { AttributeKey = "Connectivity", Value = "Wi Fi + 5G", ValueType = "string" },
            ["Network Type"] = new() { AttributeKey = "Network Type", Value = "5G", ValueType = "string" },
            ["Display Type"] = new() { AttributeKey = "Display Type", Value = "mini led", ValueType = "string" },
            ["Charge Port"] = new() { AttributeKey = "Charge Port", Value = "USB Type-C", ValueType = "string" },
            ["Color"] = new() { AttributeKey = "Color", Value = "space gray", ValueType = "string" }
        });

        Assert.Multiple(() =>
        {
            Assert.That(result["storage_capacity_gb"].Value, Is.EqualTo(1024));
            Assert.That(result["operating_system"].Value, Is.EqualTo("iPadOS"));
            Assert.That(result["connectivity"].Value, Is.EqualTo("Wi-Fi + Cellular"));
            Assert.That(result["cellular_generation"].Value, Is.EqualTo("5G"));
            Assert.That(result["display_technology"].Value, Is.EqualTo("Mini LED"));
            Assert.That(result["charging_port"].Value, Is.EqualTo("USB-C"));
            Assert.That(result["colour"].Value, Is.EqualTo("Space Gray"));
        });
    }

    [Test]
    public void Normalise_HandlesCompactTabletMeasurementLabels()
    {
        var sut = new TabletAttributeNormaliser();

        var result = sut.Normalise("tablet", new Dictionary<string, SourceAttributeValue>
        {
            ["Battery Capacity"] = new() { AttributeKey = "Battery Capacity", Value = "7600mAh", ValueType = "string" },
            ["Main Camera"] = new() { AttributeKey = "Main Camera", Value = "12MP", ValueType = "string" },
            ["Keyboard"] = new() { AttributeKey = "Keyboard", Value = "Included", ValueType = "string" },
            ["Refresh Rate"] = new() { AttributeKey = "Refresh Rate", Value = "144 Hz", ValueType = "string" }
        });

        Assert.Multiple(() =>
        {
            Assert.That(result["battery_capacity_mah"].Value, Is.EqualTo(7600));
            Assert.That(result["rear_camera_primary_mp"].Value, Is.EqualTo(12m));
            Assert.That(result["keyboard_support"].Value, Is.EqualTo(true));
            Assert.That(result["refresh_rate_hz"].Value, Is.EqualTo(144));
        });
    }
}