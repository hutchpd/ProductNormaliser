using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Normalisation;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.Normalisation)]
public sealed class LaptopAttributeNormaliserTests
{
    [Test]
    public void Normalise_MapsLaptopAliasesAndValueMappings()
    {
        var sut = new LaptopAttributeNormaliser();

        var result = sut.Normalise("laptop", new Dictionary<string, SourceAttributeValue>
        {
            ["Processor Model"] = new() { AttributeKey = "Processor Model", Value = "Intel Core Ultra 7 155H", ValueType = "string" },
            ["Drive Type"] = new() { AttributeKey = "Drive Type", Value = "solid state drive", ValueType = "string" },
            ["Screen Resolution"] = new() { AttributeKey = "Screen Resolution", Value = "FHD", ValueType = "string" },
            ["OS"] = new() { AttributeKey = "OS", Value = "windows 11 home", ValueType = "string" }
        });

        Assert.Multiple(() =>
        {
            Assert.That(result["cpu_model"].Value, Is.EqualTo("Intel Core Ultra 7 155H"));
            Assert.That(result["storage_type"].Value, Is.EqualTo("SSD"));
            Assert.That(result["native_resolution"].Value, Is.EqualTo("1080p"));
            Assert.That(result["operating_system"].Value, Is.EqualTo("Windows 11 Home"));
        });
    }

    [Test]
    public void Normalise_AppliesStorageWeightAndBatteryUnitRules()
    {
        var sut = new LaptopAttributeNormaliser();

        var result = sut.Normalise("laptop", new Dictionary<string, SourceAttributeValue>
        {
            ["Memory"] = new() { AttributeKey = "Memory", Value = "16 GB", ValueType = "string" },
            ["Storage Capacity"] = new() { AttributeKey = "Storage Capacity", Value = "1 TB", ValueType = "string" },
            ["Weight"] = new() { AttributeKey = "Weight", Value = "1250 g", ValueType = "string" },
            ["Battery Life"] = new() { AttributeKey = "Battery Life", Value = "14 hr", ValueType = "string" }
        });

        Assert.Multiple(() =>
        {
            Assert.That(result["ram_gb"].Value, Is.EqualTo(16));
            Assert.That(result["storage_capacity_gb"].Value, Is.EqualTo(1024));
            Assert.That(result["storage_capacity_gb"].Unit, Is.EqualTo("gb"));
            Assert.That(result["weight_kg"].Value, Is.EqualTo(1.25m));
            Assert.That(result["weight_kg"].Unit, Is.EqualTo("kg"));
            Assert.That(result["battery_life_hours"].Value, Is.EqualTo(14m));
            Assert.That(result["battery_life_hours"].Unit, Is.EqualTo("hour"));
        });
    }

    [Test]
    public void LaptopNormaliser_ExposesIdentityAndCompletenessKeys()
    {
        var sut = new LaptopAttributeNormaliser();

        Assert.Multiple(() =>
        {
            Assert.That(sut.IdentityAttributeKeys, Is.SupersetOf(new[] { "gtin", "brand", "model_number", "cpu_model", "display_size_inch", "storage_capacity_gb" }));
            Assert.That(sut.CompletenessAttributeKeys, Is.SupersetOf(new[] { "storage_type", "operating_system", "native_resolution" }));
        });
    }
}