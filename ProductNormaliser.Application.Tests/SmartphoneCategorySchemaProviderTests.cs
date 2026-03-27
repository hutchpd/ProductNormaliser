using System.Text.RegularExpressions;
using ProductNormaliser.Core.Normalisation;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.CategorySchema)]
public sealed class SmartphoneCategorySchemaProviderTests
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
        "storage_capacity_gb",
        "ram_gb",
        "operating_system",
        "cellular_generation",
        "sim_form_factor",
        "esim_support",
        "dual_sim",
        "carrier_lock_status",
        "screen_size_inch",
        "native_resolution",
        "display_technology",
        "refresh_rate_hz",
        "chipset_model",
        "rear_camera_primary_mp",
        "front_camera_mp",
        "battery_capacity_mah",
        "charging_port",
        "wireless_charging",
        "nfc",
        "ip_rating",
        "width_mm",
        "height_mm",
        "depth_mm",
        "weight_g"
    ];

    private static readonly string[] RequiredKeys =
    [
        "brand",
        "model_number",
        "model_family",
        "storage_capacity_gb",
        "operating_system",
        "cellular_generation",
        "screen_size_inch"
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
        "storage_capacity_gb",
        "ram_gb",
        "carrier_lock_status"
    ];

    [Test]
    public void SmartphoneSchema_ContainsExpectedCanonicalKeys()
    {
        var schema = new SmartphoneCategorySchemaProvider().GetSchema();

        Assert.Multiple(() =>
        {
            Assert.That(schema.CategoryKey, Is.EqualTo(SmartphoneCategorySchemaProvider.CategoryKey));
            Assert.That(schema.Attributes.Select(attribute => attribute.Key), Is.EquivalentTo(ExpectedKeys));
            Assert.That(schema.Attributes, Has.Count.EqualTo(ExpectedKeys.Length));
        });
    }

    [Test]
    public void SmartphoneSchema_MarksRequiredFieldsExplicitly()
    {
        var schema = new SmartphoneCategorySchemaProvider().GetSchema();
        var requiredKeys = schema.Attributes
            .Where(attribute => attribute.IsRequired)
            .Select(attribute => attribute.Key)
            .ToArray();

        Assert.That(requiredKeys, Is.EquivalentTo(RequiredKeys));
    }

    [Test]
    public void SmartphoneSchema_DoesNotContainDuplicateCanonicalKeys()
    {
        var schema = new SmartphoneCategorySchemaProvider().GetSchema();

        var duplicateKeys = schema.Attributes
            .GroupBy(attribute => attribute.Key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.That(duplicateKeys, Is.Empty);
    }

    [Test]
    public void SmartphoneSchema_UsesSnakeCaseKeys()
    {
        var schema = new SmartphoneCategorySchemaProvider().GetSchema();
        var snakeCasePattern = new Regex("^[a-z]+(?:_[a-z0-9]+)*$", RegexOptions.Compiled);

        var invalidKeys = schema.Attributes
            .Select(attribute => attribute.Key)
            .Where(key => !snakeCasePattern.IsMatch(key))
            .ToArray();

        Assert.That(invalidKeys, Is.Empty);
    }

    [Test]
    public void SmartphoneSchema_IdentityRelevantFields_AlignWithNormaliserIdentityKeys()
    {
        var normaliser = new SmartphoneAttributeNormaliser();

        Assert.That(normaliser.IdentityAttributeKeys, Is.EquivalentTo(IdentityRelevantKeys));
    }
}