using System.Text.RegularExpressions;
using ProductNormaliser.Core.Normalisation;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.CategorySchema)]
public sealed class TabletCategorySchemaProviderTests
{
    private static readonly string[] ExpectedKeys =
    [
        "brand",
        "model_number",
        "model_family",
        "variant_name",
        "manufacturer_part_number",
        "gtin",
        "regional_variant",
        "release_year",
        "colour",
        "display_size_inch",
        "native_resolution",
        "display_technology",
        "refresh_rate_hz",
        "storage_capacity_gb",
        "ram_gb",
        "operating_system",
        "connectivity",
        "cellular_generation",
        "chipset_model",
        "battery_life_hours",
        "battery_capacity_mah",
        "charging_port",
        "weight_g",
        "width_mm",
        "height_mm",
        "depth_mm",
        "stylus_support",
        "keyboard_support",
        "rear_camera_primary_mp",
        "front_camera_mp"
    ];

    private static readonly string[] RequiredKeys =
    [
        "brand",
        "model_number",
        "model_family",
        "display_size_inch",
        "storage_capacity_gb",
        "operating_system",
        "connectivity"
    ];

    private static readonly string[] IdentityRelevantKeys =
    [
        "gtin",
        "brand",
        "model_number",
        "model_family",
        "variant_name",
        "manufacturer_part_number",
        "regional_variant",
        "colour",
        "display_size_inch",
        "storage_capacity_gb",
        "ram_gb",
        "connectivity"
    ];

    [Test]
    public void TabletSchema_ContainsExpectedCanonicalKeys()
    {
        var schema = new TabletCategorySchemaProvider().GetSchema();

        Assert.Multiple(() =>
        {
            Assert.That(schema.CategoryKey, Is.EqualTo(TabletCategorySchemaProvider.CategoryKey));
            Assert.That(schema.Attributes.Select(attribute => attribute.Key), Is.EquivalentTo(ExpectedKeys));
            Assert.That(schema.Attributes, Has.Count.EqualTo(ExpectedKeys.Length));
        });
    }

    [Test]
    public void TabletSchema_MarksRequiredFieldsExplicitly()
    {
        var schema = new TabletCategorySchemaProvider().GetSchema();
        var requiredKeys = schema.Attributes
            .Where(attribute => attribute.IsRequired)
            .Select(attribute => attribute.Key)
            .ToArray();

        Assert.That(requiredKeys, Is.EquivalentTo(RequiredKeys));
    }

    [Test]
    public void TabletSchema_DoesNotContainDuplicateCanonicalKeys()
    {
        var schema = new TabletCategorySchemaProvider().GetSchema();

        var duplicateKeys = schema.Attributes
            .GroupBy(attribute => attribute.Key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.That(duplicateKeys, Is.Empty);
    }

    [Test]
    public void TabletSchema_UsesSnakeCaseKeys()
    {
        var schema = new TabletCategorySchemaProvider().GetSchema();
        var snakeCasePattern = new Regex("^[a-z]+(?:_[a-z0-9]+)*$", RegexOptions.Compiled);

        var invalidKeys = schema.Attributes
            .Select(attribute => attribute.Key)
            .Where(key => !snakeCasePattern.IsMatch(key))
            .ToArray();

        Assert.That(invalidKeys, Is.Empty);
    }

    [Test]
    public void TabletSchema_IdentityRelevantFields_AlignWithNormaliserIdentityKeys()
    {
        var normaliser = new TabletAttributeNormaliser();

        Assert.That(normaliser.IdentityAttributeKeys, Is.EquivalentTo(IdentityRelevantKeys));
    }
}