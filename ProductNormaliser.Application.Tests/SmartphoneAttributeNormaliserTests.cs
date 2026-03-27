using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Normalisation;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.Normalisation)]
public sealed class SmartphoneAttributeNormaliserTests
{
    [Test]
    public void SmartphoneNormaliser_ExposesIdentityAndCompletenessKeysForMatureSchema()
    {
        var sut = new SmartphoneAttributeNormaliser();

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
                "carrier_lock_status"
            }));
            Assert.That(sut.CompletenessAttributeKeys, Is.SupersetOf(new[]
            {
                "model_family",
                "display_technology",
                "chipset_model",
                "rear_camera_primary_mp",
                "ip_rating"
            }));
        });
    }

    [Test]
    public void Normalise_MapsRenamedRearCameraField()
    {
        var sut = new SmartphoneAttributeNormaliser();

        var result = sut.Normalise("smartphone", new Dictionary<string, SourceAttributeValue>
        {
            ["Rear Camera"] = new() { AttributeKey = "Rear Camera", Value = "50 MP", ValueType = "string" }
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.ContainsKey("rear_camera_primary_mp"), Is.True);
            Assert.That(result["rear_camera_primary_mp"].Value, Is.EqualTo(50m));
            Assert.That(result["rear_camera_primary_mp"].Unit, Is.EqualTo("mp"));
        });
    }

    [Test]
    public void Normalise_MapsRetailerAliasesAndStableEnumerations()
    {
        var sut = new SmartphoneAttributeNormaliser();

        var result = sut.Normalise("smartphone", new Dictionary<string, SourceAttributeValue>
        {
            ["Internal Storage"] = new() { AttributeKey = "Internal Storage", Value = "256 GB", ValueType = "string" },
            ["System Memory"] = new() { AttributeKey = "System Memory", Value = "8 GB", ValueType = "string" },
            ["Operating System"] = new() { AttributeKey = "Operating System", Value = "Android 14", ValueType = "string" },
            ["Network Type"] = new() { AttributeKey = "Network Type", Value = "5G NR", ValueType = "string" },
            ["SIM Type"] = new() { AttributeKey = "SIM Type", Value = "nano sim + esim", ValueType = "string" },
            ["Charge Port"] = new() { AttributeKey = "Charge Port", Value = "USB Type C", ValueType = "string" },
            ["Display Type"] = new() { AttributeKey = "Display Type", Value = "dynamic amoled 2x", ValueType = "string" },
            ["Lock Status"] = new() { AttributeKey = "Lock Status", Value = "sim-free", ValueType = "string" },
            ["Color"] = new() { AttributeKey = "Color", Value = "phantom black", ValueType = "string" },
            ["Ingress Protection"] = new() { AttributeKey = "Ingress Protection", Value = "IP 68", ValueType = "string" }
        });

        Assert.Multiple(() =>
        {
            Assert.That(result["storage_capacity_gb"].Value, Is.EqualTo(256));
            Assert.That(result["ram_gb"].Value, Is.EqualTo(8));
            Assert.That(result["operating_system"].Value, Is.EqualTo("Android"));
            Assert.That(result["cellular_generation"].Value, Is.EqualTo("5G"));
            Assert.That(result["sim_form_factor"].Value, Is.EqualTo("Nano-SIM + eSIM"));
            Assert.That(result["charging_port"].Value, Is.EqualTo("USB-C"));
            Assert.That(result["display_technology"].Value, Is.EqualTo("Dynamic AMOLED 2X"));
            Assert.That(result["carrier_lock_status"].Value, Is.EqualTo("Unlocked"));
            Assert.That(result["colour"].Value, Is.EqualTo("Phantom Black"));
            Assert.That(result["ip_rating"].Value, Is.EqualTo("IP68"));
        });
    }

    [Test]
    public void Normalise_HandlesCompactRetailerMeasurements()
    {
        var sut = new SmartphoneAttributeNormaliser();

        var result = sut.Normalise("smartphone", new Dictionary<string, SourceAttributeValue>
        {
            ["Battery Capacity"] = new() { AttributeKey = "Battery Capacity", Value = "5000mAh", ValueType = "string" },
            ["Rear Camera"] = new() { AttributeKey = "Rear Camera", Value = "50MP", ValueType = "string" },
            ["Front Camera"] = new() { AttributeKey = "Front Camera", Value = "12MP", ValueType = "string" },
            ["Screen Size"] = new() { AttributeKey = "Screen Size", Value = "17.02 cm", ValueType = "string" }
        });

        Assert.Multiple(() =>
        {
            Assert.That(result["battery_capacity_mah"].Value, Is.EqualTo(5000));
            Assert.That(result["rear_camera_primary_mp"].Value, Is.EqualTo(50m));
            Assert.That(result["front_camera_mp"].Value, Is.EqualTo(12m));
            Assert.That(result["screen_size_inch"].Value, Is.EqualTo(7m));
        });
    }
}