using System.Text.RegularExpressions;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Normalisation;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.CategorySchema)]
public sealed class TvCategorySchemaProviderTests
{
    private static readonly string[] ExpectedKeys =
    [
        "brand",
        "model_number",
        "gtin",
        "screen_size_inch",
        "native_resolution",
        "display_technology",
        "hdmi_port_count",
        "smart_tv",
        "smart_platform",
        "refresh_rate_hz",
        "vesa_mount_width_mm",
        "vesa_mount_height_mm",
        "width_mm",
        "height_mm",
        "depth_mm"
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
        "screen_size_inch",
        "native_resolution"
    ];

    [Test]
    public void TvSchema_ContainsExpectedCanonicalKeys()
    {
        var schema = new TvCategorySchemaProvider().GetSchema();

        Assert.Multiple(() =>
        {
            Assert.That(schema.CategoryKey, Is.EqualTo(TvCategorySchemaProvider.CategoryKey));
            Assert.That(schema.Attributes.Select(attribute => attribute.Key), Is.EquivalentTo(ExpectedKeys));
            Assert.That(schema.Attributes, Has.Count.EqualTo(ExpectedKeys.Length));
        });
    }

    [Test]
    public void TvSchema_MarksRequiredFieldsExplicitly()
    {
        var schema = new TvCategorySchemaProvider().GetSchema();
        var requiredKeys = schema.Attributes
            .Where(attribute => attribute.IsRequired)
            .Select(attribute => attribute.Key)
            .ToArray();

        Assert.That(requiredKeys, Is.EquivalentTo(RequiredKeys));
    }

    [Test]
    public void TvSchema_DoesNotContainDuplicateCanonicalKeys()
    {
        var schema = new TvCategorySchemaProvider().GetSchema();

        var duplicateKeys = schema.Attributes
            .GroupBy(attribute => attribute.Key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.That(duplicateKeys, Is.Empty);
    }

    [Test]
    public void TvSchema_UsesSnakeCaseKeys()
    {
        var schema = new TvCategorySchemaProvider().GetSchema();
        var snakeCasePattern = new Regex("^[a-z]+(?:_[a-z0-9]+)*$", RegexOptions.Compiled);

        var invalidKeys = schema.Attributes
            .Select(attribute => attribute.Key)
            .Where(key => !snakeCasePattern.IsMatch(key))
            .ToArray();

        Assert.That(invalidKeys, Is.Empty);
    }

    [Test]
    public void TvSchema_IdentityRelevantFields_AlignWithNormaliserIdentityKeys()
    {
        var normaliser = new TvAttributeNormaliser();

        Assert.That(normaliser.IdentityAttributeKeys, Is.EquivalentTo(IdentityRelevantKeys));
    }

    [Test]
    public void TvSchema_AssignsConflictSensitivityForMergeCriticalFields()
    {
        var schema = new TvCategorySchemaProvider().GetSchema();

        Assert.Multiple(() =>
        {
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "brand").ConflictSensitivity, Is.EqualTo(ConflictSensitivity.Critical));
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "native_resolution").ConflictSensitivity, Is.EqualTo(ConflictSensitivity.High));
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "smart_platform").ConflictSensitivity, Is.EqualTo(ConflictSensitivity.Low));
        });
    }
}