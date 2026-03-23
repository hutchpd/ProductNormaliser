using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Tests;

public sealed class MonitorCategorySchemaProviderTests
{
    [Test]
    public void MonitorSchema_ContainsRolloutAttributesAndSensitivity()
    {
        var schema = new MonitorCategorySchemaProvider().GetSchema();

        Assert.Multiple(() =>
        {
            Assert.That(schema.CategoryKey, Is.EqualTo("monitor"));
            Assert.That(schema.Attributes.Select(attribute => attribute.Key), Is.SupersetOf(new[]
            {
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
                "vesa_mount_height_mm"
            }));
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "brand").ConflictSensitivity, Is.EqualTo(ConflictSensitivity.Critical));
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "refresh_rate_hz").ConflictSensitivity, Is.EqualTo(ConflictSensitivity.High));
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "width_mm").ConflictSensitivity, Is.EqualTo(ConflictSensitivity.Low));
        });
    }
}