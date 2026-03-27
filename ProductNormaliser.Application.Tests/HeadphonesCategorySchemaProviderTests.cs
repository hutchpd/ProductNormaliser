using System.Text.RegularExpressions;
using ProductNormaliser.Core.Normalisation;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.CategorySchema)]
public sealed class HeadphonesCategorySchemaProviderTests
{
    private static readonly string[] ExpectedKeys =
    [
        "brand",
        "model_number",
        "model_family",
        "variant_name",
        "manufacturer_part_number",
        "gtin",
        "release_year",
        "colour",
        "form_factor",
        "connection_type",
        "wireless",
        "bluetooth_version",
        "multipoint_support",
        "noise_cancelling",
        "battery_life_hours",
        "case_battery_life_hours",
        "charging_port",
        "driver_size_mm",
        "impedance_ohm",
        "microphone",
        "ip_rating",
        "weight_g"
    ];

    private static readonly string[] RequiredKeys =
    [
        "brand",
        "model_number",
        "model_family",
        "form_factor",
        "connection_type"
    ];

    private static readonly string[] IdentityRelevantKeys =
    [
        "gtin",
        "brand",
        "model_number",
        "model_family",
        "variant_name",
        "manufacturer_part_number",
        "colour",
        "form_factor",
        "connection_type"
    ];

    [Test]
    public void HeadphonesSchema_ContainsExpectedCanonicalKeys()
    {
        var schema = new HeadphonesCategorySchemaProvider().GetSchema();

        Assert.Multiple(() =>
        {
            Assert.That(schema.CategoryKey, Is.EqualTo(HeadphonesCategorySchemaProvider.CategoryKey));
            Assert.That(schema.Attributes.Select(attribute => attribute.Key), Is.EquivalentTo(ExpectedKeys));
            Assert.That(schema.Attributes, Has.Count.EqualTo(ExpectedKeys.Length));
        });
    }

    [Test]
    public void HeadphonesSchema_MarksRequiredFieldsExplicitly()
    {
        var schema = new HeadphonesCategorySchemaProvider().GetSchema();
        var requiredKeys = schema.Attributes
            .Where(attribute => attribute.IsRequired)
            .Select(attribute => attribute.Key)
            .ToArray();

        Assert.That(requiredKeys, Is.EquivalentTo(RequiredKeys));
    }

    [Test]
    public void HeadphonesSchema_DoesNotContainDuplicateCanonicalKeys()
    {
        var schema = new HeadphonesCategorySchemaProvider().GetSchema();

        var duplicateKeys = schema.Attributes
            .GroupBy(attribute => attribute.Key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.That(duplicateKeys, Is.Empty);
    }

    [Test]
    public void HeadphonesSchema_UsesSnakeCaseKeys()
    {
        var schema = new HeadphonesCategorySchemaProvider().GetSchema();
        var snakeCasePattern = new Regex("^[a-z]+(?:_[a-z0-9]+)*$", RegexOptions.Compiled);

        var invalidKeys = schema.Attributes
            .Select(attribute => attribute.Key)
            .Where(key => !snakeCasePattern.IsMatch(key))
            .ToArray();

        Assert.That(invalidKeys, Is.Empty);
    }

    [Test]
    public void HeadphonesSchema_IdentityRelevantFields_AlignWithNormaliserIdentityKeys()
    {
        var normaliser = new HeadphonesAttributeNormaliser();

        Assert.That(normaliser.IdentityAttributeKeys, Is.EquivalentTo(IdentityRelevantKeys));
    }
}