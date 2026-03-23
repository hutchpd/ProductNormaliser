using System.Text.RegularExpressions;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Tests;

public sealed class TvCategorySchemaProviderTests
{
    private static readonly string[] RequiredKeys =
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

    [Test]
    public void TvSchemaExists()
    {
        var provider = new TvCategorySchemaProvider();

        var schema = provider.GetSchema();

        Assert.Multiple(() =>
        {
            Assert.That(schema, Is.Not.Null);
            Assert.That(schema.CategoryKey, Is.EqualTo(TvCategorySchemaProvider.CategoryKey));
            Assert.That(schema.Attributes, Is.Not.Empty);
        });
    }

    [Test]
    public void TvSchemaContainsRequiredCanonicalKeys()
    {
        var provider = new TvCategorySchemaProvider();
        var schema = provider.GetSchema();
        var keys = schema.Attributes.Select(attribute => attribute.Key).ToArray();

        Assert.That(keys, Is.SupersetOf(RequiredKeys));
    }

    [Test]
    public void TvSchemaDoesNotContainDuplicateCanonicalKeys()
    {
        var provider = new TvCategorySchemaProvider();
        var schema = provider.GetSchema();

        var duplicateKeys = schema.Attributes
            .GroupBy(attribute => attribute.Key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.That(duplicateKeys, Is.Empty);
    }

    [Test]
    public void TvSchemaKeysUseSnakeCase()
    {
        var provider = new TvCategorySchemaProvider();
        var schema = provider.GetSchema();
        var snakeCasePattern = new Regex("^[a-z]+(?:_[a-z0-9]+)*$", RegexOptions.Compiled);

        var invalidKeys = schema.Attributes
            .Select(attribute => attribute.Key)
            .Where(key => !snakeCasePattern.IsMatch(key))
            .ToArray();

        Assert.That(invalidKeys, Is.Empty);
    }

    [Test]
    public void TvSchema_AssignsConflictSensitivityForMergeCriticalFields()
    {
        var schema = new TvCategorySchemaProvider().GetSchema();

        Assert.Multiple(() =>
        {
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "brand").ConflictSensitivity, Is.EqualTo(ProductNormaliser.Core.Models.ConflictSensitivity.Critical));
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "native_resolution").ConflictSensitivity, Is.EqualTo(ProductNormaliser.Core.Models.ConflictSensitivity.High));
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "smart_platform").ConflictSensitivity, Is.EqualTo(ProductNormaliser.Core.Models.ConflictSensitivity.Low));
        });
    }
}