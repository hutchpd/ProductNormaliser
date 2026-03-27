using System.Text.RegularExpressions;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Normalisation;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.CategorySchema)]
public sealed class LaptopCategorySchemaProviderTests
{
    private static readonly string[] ExpectedKeys =
    [
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
    ];

    private static readonly string[] RequiredKeys =
    [
        "brand",
        "model_number"
    ];

    private static readonly string[] IdentityRelevantKeys =
    [
        "gtin",
        "brand",
        "model_number",
        "cpu_model",
        "display_size_inch",
        "storage_capacity_gb"
    ];

    [Test]
    public void LaptopSchema_ContainsExpectedCanonicalKeys()
    {
        var schema = new LaptopCategorySchemaProvider().GetSchema();

        Assert.Multiple(() =>
        {
            Assert.That(schema.CategoryKey, Is.EqualTo("laptop"));
            Assert.That(schema.Attributes.Select(attribute => attribute.Key), Is.EquivalentTo(ExpectedKeys));
            Assert.That(schema.Attributes, Has.Count.EqualTo(ExpectedKeys.Length));
        });
    }

    [Test]
    public void LaptopSchema_MarksRequiredFieldsExplicitly()
    {
        var schema = new LaptopCategorySchemaProvider().GetSchema();
        var requiredKeys = schema.Attributes
            .Where(attribute => attribute.IsRequired)
            .Select(attribute => attribute.Key)
            .ToArray();

        Assert.That(requiredKeys, Is.EquivalentTo(RequiredKeys));
    }

    [Test]
    public void LaptopSchema_DoesNotContainDuplicateCanonicalKeys()
    {
        var schema = new LaptopCategorySchemaProvider().GetSchema();

        var duplicateKeys = schema.Attributes
            .GroupBy(attribute => attribute.Key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.That(duplicateKeys, Is.Empty);
    }

    [Test]
    public void LaptopSchema_UsesSnakeCaseKeys()
    {
        var schema = new LaptopCategorySchemaProvider().GetSchema();
        var snakeCasePattern = new Regex("^[a-z]+(?:_[a-z0-9]+)*$", RegexOptions.Compiled);

        var invalidKeys = schema.Attributes
            .Select(attribute => attribute.Key)
            .Where(key => !snakeCasePattern.IsMatch(key))
            .ToArray();

        Assert.That(invalidKeys, Is.Empty);
    }

    [Test]
    public void LaptopSchema_IdentityRelevantFields_AlignWithNormaliserIdentityKeys()
    {
        var normaliser = new LaptopAttributeNormaliser();

        Assert.That(normaliser.IdentityAttributeKeys, Is.EquivalentTo(IdentityRelevantKeys));
    }

    [Test]
    public void LaptopSchema_AssignsConflictSensitivityForMergeCriticalFields()
    {
        var schema = new LaptopCategorySchemaProvider().GetSchema();

        Assert.Multiple(() =>
        {
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "model_number").ConflictSensitivity, Is.EqualTo(ConflictSensitivity.Critical));
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "storage_capacity_gb").ConflictSensitivity, Is.EqualTo(ConflictSensitivity.High));
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "operating_system").ConflictSensitivity, Is.EqualTo(ConflictSensitivity.Low));
        });
    }
}