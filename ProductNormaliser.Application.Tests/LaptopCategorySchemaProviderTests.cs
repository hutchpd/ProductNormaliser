using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.CategorySchema)]
public sealed class LaptopCategorySchemaProviderTests
{
    [Test]
    public void LaptopSchema_ContainsRolloutAttributesAndSensitivity()
    {
        var schema = new LaptopCategorySchemaProvider().GetSchema();

        Assert.Multiple(() =>
        {
            Assert.That(schema.CategoryKey, Is.EqualTo("laptop"));
            Assert.That(schema.Attributes.Select(attribute => attribute.Key), Is.SupersetOf(new[]
            {
                "brand",
                "model_number",
                "gtin",
                "cpu_model",
                "ram_gb",
                "storage_capacity_gb",
                "storage_type",
                "display_size_inch",
                "native_resolution",
                "graphics_model",
                "operating_system",
                "battery_life_hours",
                "weight_kg"
            }));
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "model_number").ConflictSensitivity, Is.EqualTo(ConflictSensitivity.Critical));
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "storage_capacity_gb").ConflictSensitivity, Is.EqualTo(ConflictSensitivity.High));
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "operating_system").ConflictSensitivity, Is.EqualTo(ConflictSensitivity.Low));
        });
    }
}