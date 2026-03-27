using System.Text.RegularExpressions;
using ProductNormaliser.Core.Normalisation;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.CategorySchema)]
public sealed class SpeakersCategorySchemaProviderTests
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
        "speaker_type",
        "connection_type",
        "wireless",
        "bluetooth_version",
        "battery_life_hours",
        "power_output_w",
        "voice_assistant",
        "smart_platform",
        "water_resistant",
        "ip_rating",
        "stereo_pairing",
        "multiroom_support",
        "charging_port",
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
        "speaker_type",
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
        "speaker_type",
        "connection_type"
    ];

    [Test]
    public void SpeakersSchema_ContainsExpectedCanonicalKeys()
    {
        var schema = new SpeakersCategorySchemaProvider().GetSchema();

        Assert.Multiple(() =>
        {
            Assert.That(schema.CategoryKey, Is.EqualTo(SpeakersCategorySchemaProvider.CategoryKey));
            Assert.That(schema.Attributes.Select(attribute => attribute.Key), Is.EquivalentTo(ExpectedKeys));
            Assert.That(schema.Attributes, Has.Count.EqualTo(ExpectedKeys.Length));
        });
    }

    [Test]
    public void SpeakersSchema_MarksRequiredFieldsExplicitly()
    {
        var schema = new SpeakersCategorySchemaProvider().GetSchema();
        var requiredKeys = schema.Attributes
            .Where(attribute => attribute.IsRequired)
            .Select(attribute => attribute.Key)
            .ToArray();

        Assert.That(requiredKeys, Is.EquivalentTo(RequiredKeys));
    }

    [Test]
    public void SpeakersSchema_DoesNotContainDuplicateCanonicalKeys()
    {
        var schema = new SpeakersCategorySchemaProvider().GetSchema();

        var duplicateKeys = schema.Attributes
            .GroupBy(attribute => attribute.Key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.That(duplicateKeys, Is.Empty);
    }

    [Test]
    public void SpeakersSchema_UsesSnakeCaseKeys()
    {
        var schema = new SpeakersCategorySchemaProvider().GetSchema();
        var snakeCasePattern = new Regex("^[a-z]+(?:_[a-z0-9]+)*$", RegexOptions.Compiled);

        var invalidKeys = schema.Attributes
            .Select(attribute => attribute.Key)
            .Where(key => !snakeCasePattern.IsMatch(key))
            .ToArray();

        Assert.That(invalidKeys, Is.Empty);
    }

    [Test]
    public void SpeakersSchema_IdentityRelevantFields_AlignWithNormaliserIdentityKeys()
    {
        var normaliser = new SpeakersAttributeNormaliser();

        Assert.That(normaliser.IdentityAttributeKeys, Is.EquivalentTo(IdentityRelevantKeys));
    }
}