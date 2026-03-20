using ProductNormaliser.Core.Merging;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Normalisation;

namespace ProductNormaliser.Tests;

public sealed class CategoryAttributeNormaliserRegistryTests
{
    [Test]
    public void Registry_DispatchesToExpectedCategoryProviders()
    {
        var registry = CreateRegistry();

        Assert.Multiple(() =>
        {
            Assert.That(registry.GetProvider("tv"), Is.TypeOf<TvAttributeNormaliser>());
            Assert.That(registry.GetProvider("monitor"), Is.TypeOf<MonitorAttributeNormaliser>());
            Assert.That(registry.GetProvider("laptop"), Is.TypeOf<LaptopAttributeNormaliser>());
            Assert.That(registry.GetProvider("refrigerator"), Is.TypeOf<RefrigeratorAttributeNormaliser>());
        });
    }

    [Test]
    public void Registry_HandlesUnknownCategoriesSafely()
    {
        var registry = CreateRegistry();
        var result = registry.Normalise("smartwatch", new Dictionary<string, SourceAttributeValue>
        {
            ["Screen Size"] = new()
            {
                AttributeKey = "Screen Size",
                Value = "1.9 in",
                ValueType = "string"
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(registry.GetProvider("smartwatch"), Is.Null);
            Assert.That(registry.GetIdentityAttributeKeys("smartwatch"), Is.Empty);
            Assert.That(registry.GetCompletenessAttributeKeys("smartwatch"), Is.Empty);
            Assert.That(result, Is.Empty);
        });
    }

    [Test]
    public void Registry_NormalisesMonitorAndLaptopAttributesDifferently()
    {
        var registry = CreateRegistry();

        var monitorResult = registry.Normalise("monitor", new Dictionary<string, SourceAttributeValue>
        {
            ["Panel Technology"] = new()
            {
                AttributeKey = "Panel Technology",
                Value = "ips",
                ValueType = "string"
            },
            ["Display Size"] = new()
            {
                AttributeKey = "Display Size",
                Value = "68.6 cm",
                ValueType = "string"
            }
        });

        var laptopResult = registry.Normalise("laptop", new Dictionary<string, SourceAttributeValue>
        {
            ["Display Size"] = new()
            {
                AttributeKey = "Display Size",
                Value = "15.6 in",
                ValueType = "string"
            },
            ["Memory"] = new()
            {
                AttributeKey = "Memory",
                Value = "16 GB",
                ValueType = "string"
            },
            ["Storage"] = new()
            {
                AttributeKey = "Storage",
                Value = "512 GB",
                ValueType = "string"
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(monitorResult["panel_type"].Value, Is.EqualTo("IPS"));
            Assert.That(monitorResult["screen_size_inch"].Value, Is.EqualTo(27m));
            Assert.That(laptopResult["display_size_inch"].Value, Is.EqualTo(16m));
            Assert.That(laptopResult["ram_gb"].Value, Is.EqualTo(16));
            Assert.That(laptopResult["storage_capacity_gb"].Value, Is.EqualTo(512));
        });
    }

    [Test]
    public void IdentityResolver_UsesCategorySpecificIdentityAttributes()
    {
        var registry = CreateRegistry();
        var resolver = new ProductIdentityResolver(categoryAttributeNormaliserRegistry: registry);
        var sourceProduct = new SourceProduct
        {
            Id = "source-monitor-1",
            SourceName = "display-source",
            SourceUrl = "https://example.com/monitor",
            CategoryKey = "monitor",
            Brand = "Dell",
            Title = "Dell UltraSharp 27 Monitor",
            RawSchemaJson = "{}",
            FetchedUtc = new DateTime(2026, 03, 20, 11, 00, 00, DateTimeKind.Utc),
            NormalisedAttributes = registry.Normalise("monitor", new Dictionary<string, SourceAttributeValue>
            {
                ["Display Size"] = new()
                {
                    AttributeKey = "Display Size",
                    Value = "27 in",
                    ValueType = "string"
                },
                ["Native Resolution"] = new()
                {
                    AttributeKey = "Native Resolution",
                    Value = "QHD",
                    ValueType = "string"
                },
                ["Panel Type"] = new()
                {
                    AttributeKey = "Panel Type",
                    Value = "IPS",
                    ValueType = "string"
                }
            })
        };
        var candidates = new[]
        {
            new CanonicalProduct
            {
                Id = "monitor-1",
                CategoryKey = "monitor",
                Brand = "Dell",
                DisplayName = "Dell 27-inch monitor",
                Attributes = new Dictionary<string, CanonicalAttributeValue>
                {
                    ["screen_size_inch"] = new() { AttributeKey = "screen_size_inch", Value = 27m, ValueType = "decimal" },
                    ["native_resolution"] = new() { AttributeKey = "native_resolution", Value = "1440p", ValueType = "string" },
                    ["panel_type"] = new() { AttributeKey = "panel_type", Value = "IPS", ValueType = "string" }
                }
            }
        };

        var result = resolver.Match(sourceProduct, candidates);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsMatch, Is.True);
            Assert.That(result.CanonicalProductId, Is.EqualTo("monitor-1"));
            Assert.That(result.Confidence, Is.EqualTo(0.93m));
            Assert.That(result.MatchReason, Is.EqualTo("Exact match across 3 category identity attributes."));
        });
    }

    private static CategoryAttributeNormaliserRegistry CreateRegistry()
    {
        return new CategoryAttributeNormaliserRegistry([
            new TvAttributeNormaliser(),
            new MonitorAttributeNormaliser(),
            new LaptopAttributeNormaliser(),
            new RefrigeratorAttributeNormaliser()
        ]);
    }
}