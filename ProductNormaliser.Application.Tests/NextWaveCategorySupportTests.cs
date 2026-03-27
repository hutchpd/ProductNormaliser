using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Normalisation;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.CategorySchema)]
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
                "model_family",
                "display_size_inch",
                "display_technology",
                "storage_capacity_gb",
                "ram_gb",
                "operating_system",
                "connectivity",
                "chipset_model",
                "keyboard_support",
                "stylus_support"
            }));
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "model_family").IsRequired, Is.True);
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
                "model_family",
                "manufacturer_part_number",
                "regional_variant",
                "colour",
                "screen_size_inch",
                "cellular_generation",
                "carrier_lock_status",
                "display_technology",
                "rear_camera_primary_mp",
                "battery_capacity_mah",
                "dual_sim",
                "ip_rating"
            }));
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "model_family").IsRequired, Is.True);
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
                "model_family",
                "colour",
                "form_factor",
                "connection_type",
                "wireless",
                "bluetooth_version",
                "noise_cancelling",
                "case_battery_life_hours",
                "charging_port",
                "driver_size_mm"
            }));
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "model_family").IsRequired, Is.True);
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
                "model_family",
                "colour",
                "speaker_type",
                "connection_type",
                "wireless",
                "bluetooth_version",
                "power_output_w",
                "voice_assistant",
                "smart_platform",
                "stereo_pairing",
                "multiroom_support"
            }));
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "model_family").IsRequired, Is.True);
        });
    }

    [Test]
    public void NextWaveNormalisers_ExposeCategorySpecificCompletenessKeys()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new TabletAttributeNormaliser().CompletenessAttributeKeys, Is.SupersetOf(new[] { "display_technology", "chipset_model", "keyboard_support" }));
            Assert.That(new SmartphoneAttributeNormaliser().CompletenessAttributeKeys, Is.SupersetOf(new[] { "model_family", "cellular_generation", "rear_camera_primary_mp" }));
            Assert.That(new HeadphonesAttributeNormaliser().CompletenessAttributeKeys, Is.SupersetOf(new[] { "bluetooth_version", "case_battery_life_hours", "ip_rating" }));
            Assert.That(new SpeakersAttributeNormaliser().CompletenessAttributeKeys, Is.SupersetOf(new[] { "smart_platform", "stereo_pairing", "multiroom_support" }));
        });
    }
}