using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Normalisation;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Tests;

public sealed class NextWaveCategorySupportTests
{
    [Test]
    public void TabletSchema_ContainsMobileProductCoreAttributes()
    {
        var schema = new TabletCategorySchemaProvider().GetSchema();

        Assert.Multiple(() =>
        {
            Assert.That(schema.CategoryKey, Is.EqualTo("tablet"));
            Assert.That(schema.Attributes.Select(attribute => attribute.Key), Is.SupersetOf(new[]
            {
                "brand",
                "model_number",
                "display_size_inch",
                "storage_capacity_gb",
                "ram_gb",
                "operating_system",
                "connectivity",
                "stylus_support"
            }));
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "display_size_inch").ConflictSensitivity, Is.EqualTo(ConflictSensitivity.High));
        });
    }

    [Test]
    public void SmartphoneSchema_ContainsPhoneSpecificAttributes()
    {
        var schema = new SmartphoneCategorySchemaProvider().GetSchema();

        Assert.Multiple(() =>
        {
            Assert.That(schema.CategoryKey, Is.EqualTo("smartphone"));
            Assert.That(schema.Attributes.Select(attribute => attribute.Key), Is.SupersetOf(new[]
            {
                "screen_size_inch",
                "cellular_generation",
                "rear_camera_mp",
                "battery_capacity_mah",
                "dual_sim"
            }));
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "screen_size_inch").IsRequired, Is.True);
        });
    }

    [Test]
    public void HeadphonesSchema_ContainsAudioAccessoryAttributes()
    {
        var schema = new HeadphonesCategorySchemaProvider().GetSchema();

        Assert.Multiple(() =>
        {
            Assert.That(schema.CategoryKey, Is.EqualTo("headphones"));
            Assert.That(schema.Attributes.Select(attribute => attribute.Key), Is.SupersetOf(new[]
            {
                "form_factor",
                "connection_type",
                "wireless",
                "noise_cancelling",
                "driver_size_mm"
            }));
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "form_factor").IsRequired, Is.True);
        });
    }

    [Test]
    public void SpeakersSchema_ContainsPortableAndSmartSpeakerAttributes()
    {
        var schema = new SpeakersCategorySchemaProvider().GetSchema();

        Assert.Multiple(() =>
        {
            Assert.That(schema.CategoryKey, Is.EqualTo("speakers"));
            Assert.That(schema.Attributes.Select(attribute => attribute.Key), Is.SupersetOf(new[]
            {
                "speaker_type",
                "connection_type",
                "wireless",
                "power_output_w",
                "voice_assistant"
            }));
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "connection_type").IsRequired, Is.True);
        });
    }

    [Test]
    public void NextWaveNormalisers_ExposeCategorySpecificCompletenessKeys()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new TabletAttributeNormaliser().CompletenessAttributeKeys, Is.SupersetOf(new[] { "connectivity", "storage_capacity_gb" }));
            Assert.That(new SmartphoneAttributeNormaliser().CompletenessAttributeKeys, Is.SupersetOf(new[] { "cellular_generation", "rear_camera_mp" }));
            Assert.That(new HeadphonesAttributeNormaliser().CompletenessAttributeKeys, Is.SupersetOf(new[] { "noise_cancelling", "battery_life_hours" }));
            Assert.That(new SpeakersAttributeNormaliser().CompletenessAttributeKeys, Is.SupersetOf(new[] { "power_output_w", "voice_assistant" }));
        });
    }
}