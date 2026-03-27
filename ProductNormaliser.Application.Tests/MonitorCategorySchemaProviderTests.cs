using System.Text.RegularExpressions;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Normalisation;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.CategorySchema)]
public sealed class MonitorCategorySchemaProviderTests
{
    private static readonly string[] ExpectedKeys =
    [
        "brand",
        "model_number",
        "gtin",
        "screen_size_inch",
        "native_resolution",
        "panel_type",
        "refresh_rate_hz",
        "hdmi_port_count",
        "displayport_port_count",
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
        "native_resolution",
        "panel_type"
    ];

    [Test]
    public void MonitorSchema_ContainsExpectedCanonicalKeys()
    {
        var schema = new MonitorCategorySchemaProvider().GetSchema();

        Assert.Multiple(() =>
        {
            Assert.That(schema.CategoryKey, Is.EqualTo("monitor"));
            Assert.That(schema.Attributes.Select(attribute => attribute.Key), Is.EquivalentTo(ExpectedKeys));
            Assert.That(schema.Attributes, Has.Count.EqualTo(ExpectedKeys.Length));
        });
    }

    [Test]
    public void MonitorSchema_MarksRequiredFieldsExplicitly()
    {
        var schema = new MonitorCategorySchemaProvider().GetSchema();
        var requiredKeys = schema.Attributes
            .Where(attribute => attribute.IsRequired)
            .Select(attribute => attribute.Key)
            .ToArray();

        Assert.That(requiredKeys, Is.EquivalentTo(RequiredKeys));
    }

    [Test]
    public void MonitorSchema_DoesNotContainDuplicateCanonicalKeys()
    {
        var schema = new MonitorCategorySchemaProvider().GetSchema();

        var duplicateKeys = schema.Attributes
            .GroupBy(attribute => attribute.Key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.That(duplicateKeys, Is.Empty);
    }

    [Test]
    public void MonitorSchema_UsesSnakeCaseKeys()
    {
        var schema = new MonitorCategorySchemaProvider().GetSchema();
        var snakeCasePattern = new Regex("^[a-z]+(?:_[a-z0-9]+)*$", RegexOptions.Compiled);

        var invalidKeys = schema.Attributes
            .Select(attribute => attribute.Key)
            .Where(key => !snakeCasePattern.IsMatch(key))
            .ToArray();

        Assert.That(invalidKeys, Is.Empty);
    }

    [Test]
    public void MonitorSchema_IdentityRelevantFields_AlignWithNormaliserIdentityKeys()
    {
        var normaliser = new MonitorAttributeNormaliser();

        Assert.That(normaliser.IdentityAttributeKeys, Is.EquivalentTo(IdentityRelevantKeys));
    }

    [Test]
    public void MonitorSchema_AssignsConflictSensitivityForMergeCriticalFields()
    {
        var schema = new MonitorCategorySchemaProvider().GetSchema();

        Assert.Multiple(() =>
        {
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "brand").ConflictSensitivity, Is.EqualTo(ConflictSensitivity.Critical));
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "refresh_rate_hz").ConflictSensitivity, Is.EqualTo(ConflictSensitivity.High));
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "width_mm").ConflictSensitivity, Is.EqualTo(ConflictSensitivity.Low));
        });
    }
}