using ProductNormaliser.AdminApi.Services;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Mongo;
using ProductNormaliser.Infrastructure.Mongo.Repositories;

namespace ProductNormaliser.Tests;

public sealed class DataIntelligenceTests
{
    [SetUp]
    public async Task SetUpAsync()
    {
        var context = MongoIntegrationTestFixture.Context;
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.CanonicalProducts);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.SourceProducts);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.UnmappedAttributes);
        await context.EnsureIndexesAsync();
    }

    [Test]
    public async Task GetUnmappedAttributes_ReturnsAggregatedBacklog()
    {
        var context = MongoIntegrationTestFixture.Context;
        var unmappedStore = new UnmappedAttributeRepository(context);
        unmappedStore.Record("tv", "panel_depth", new SourceAttributeValue
        {
            AttributeKey = "Panel Depth",
            Value = "42 mm",
            ValueType = "string",
            SourcePath = "source:alpha|jsonld.additionalProperty"
        });
        unmappedStore.Record("tv", "panel_depth", new SourceAttributeValue
        {
            AttributeKey = "Panel Depth",
            Value = "43 mm",
            ValueType = "string",
            SourcePath = "source:beta|jsonld.additionalProperty"
        });
        unmappedStore.Record("tv", "stand_colour", new SourceAttributeValue
        {
            AttributeKey = "Stand Colour",
            Value = "Silver",
            ValueType = "string",
            SourcePath = "source:alpha|jsonld.additionalProperty"
        });

        var service = new DataIntelligenceService(context, unmappedStore);

        var result = await service.GetUnmappedAttributesAsync("tv", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].CanonicalKey, Is.EqualTo("panel_depth"));
            Assert.That(result[0].OccurrenceCount, Is.EqualTo(2));
            Assert.That(result[0].SourceNames, Is.EqualTo(new[] { "alpha", "beta" }));
            Assert.That(result[0].SampleValues, Is.EquivalentTo(new[] { "42 mm", "43 mm" }));
        });
    }

    [Test]
    public async Task DetailedCoverageAndSourceQuality_AreCalculatedAndRanked()
    {
        var context = MongoIntegrationTestFixture.Context;

        await context.SourceProducts.InsertManyAsync(
        [
            new SourceProduct
            {
                Id = "source-alpha-1",
                SourceName = "alpha",
                SourceUrl = "https://alpha.example/tv/1",
                CategoryKey = "tv",
                Brand = "Sony",
                ModelNumber = "A95L",
                Title = "Sony A95L",
                RawSchemaJson = "{}",
                FetchedUtc = DateTime.UtcNow,
                NormalisedAttributes = new Dictionary<string, NormalisedAttributeValue>
                {
                    ["screen_size_inch"] = CreateNormalisedAttribute("screen_size_inch", 55m, "decimal", 0.99m, "inch"),
                    ["native_resolution"] = CreateNormalisedAttribute("native_resolution", "4K", "string", 0.98m),
                    ["display_technology"] = CreateNormalisedAttribute("display_technology", "OLED", "string", 0.98m),
                    ["smart_tv"] = CreateNormalisedAttribute("smart_tv", true, "boolean", 0.97m)
                }
            },
            new SourceProduct
            {
                Id = "source-beta-1",
                SourceName = "beta",
                SourceUrl = "https://beta.example/tv/1",
                CategoryKey = "tv",
                Brand = "LG",
                ModelNumber = "C4",
                Title = "LG C4",
                RawSchemaJson = "{}",
                FetchedUtc = DateTime.UtcNow,
                NormalisedAttributes = new Dictionary<string, NormalisedAttributeValue>
                {
                    ["screen_size_inch"] = CreateNormalisedAttribute("screen_size_inch", 60m, "decimal", 0.60m, "inch")
                }
            }
        ]);

        await context.CanonicalProducts.InsertManyAsync(
        [
            new CanonicalProduct
            {
                Id = "canonical-1",
                CategoryKey = "tv",
                Brand = "Sony",
                ModelNumber = "A95L",
                Gtin = "1111111111111",
                DisplayName = "Sony A95L",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                Sources =
                [
                    new ProductSourceLink
                    {
                        SourceName = "alpha",
                        SourceProductId = "source-alpha-1",
                        SourceUrl = "https://alpha.example/tv/1",
                        FirstSeenUtc = DateTime.UtcNow,
                        LastSeenUtc = DateTime.UtcNow
                    }
                ],
                Attributes = new Dictionary<string, CanonicalAttributeValue>
                {
                    ["screen_size_inch"] = CreateCanonicalAttribute("screen_size_inch", 55m, "decimal", 0.99m, "inch"),
                    ["native_resolution"] = CreateCanonicalAttribute("native_resolution", "4K", "string", 0.99m),
                    ["display_technology"] = CreateCanonicalAttribute("display_technology", "OLED", "string", 0.98m),
                    ["smart_tv"] = CreateCanonicalAttribute("smart_tv", true, "boolean", 0.97m),
                    ["smart_platform"] = CreateCanonicalAttribute("smart_platform", "Google TV", "string", 0.96m),
                    ["refresh_rate_hz"] = CreateCanonicalAttribute("refresh_rate_hz", 120, "integer", 0.95m, "hz"),
                    ["vesa_mount_width_mm"] = CreateCanonicalAttribute("vesa_mount_width_mm", 300, "integer", 0.94m, "mm"),
                    ["vesa_mount_height_mm"] = CreateCanonicalAttribute("vesa_mount_height_mm", 300, "integer", 0.94m, "mm"),
                    ["width_mm"] = CreateCanonicalAttribute("width_mm", 1225m, "decimal", 0.94m, "mm"),
                    ["height_mm"] = CreateCanonicalAttribute("height_mm", 705m, "decimal", 0.94m, "mm"),
                    ["depth_mm"] = CreateCanonicalAttribute("depth_mm", 45m, "decimal", 0.94m, "mm")
                }
            },
            new CanonicalProduct
            {
                Id = "canonical-2",
                CategoryKey = "tv",
                Brand = "LG",
                ModelNumber = "C4",
                DisplayName = "LG C4",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                Sources =
                [
                    new ProductSourceLink
                    {
                        SourceName = "beta",
                        SourceProductId = "source-beta-1",
                        SourceUrl = "https://beta.example/tv/1",
                        FirstSeenUtc = DateTime.UtcNow,
                        LastSeenUtc = DateTime.UtcNow
                    }
                ],
                Attributes = new Dictionary<string, CanonicalAttributeValue>
                {
                    ["screen_size_inch"] = CreateCanonicalAttribute("screen_size_inch", 65m, "decimal", 0.70m, "inch", hasConflict: true)
                }
            }
        ]);

        var service = new DataIntelligenceService(context, new UnmappedAttributeRepository(context));

        var coverage = await service.GetDetailedCoverageAsync("tv", CancellationToken.None);
        var sourceScores = await service.GetSourceQualityScoresAsync("tv", CancellationToken.None);

        var screenSize = coverage.Attributes.Single(attribute => attribute.AttributeKey == "screen_size_inch");
        var nativeResolution = coverage.Attributes.Single(attribute => attribute.AttributeKey == "native_resolution");

        Assert.Multiple(() =>
        {
            Assert.That(coverage.TotalCanonicalProducts, Is.EqualTo(2));
            Assert.That(coverage.TotalSourceProducts, Is.EqualTo(2));
            Assert.That(coverage.MostMissingAttributes[0].AttributeKey, Is.EqualTo("hdmi_port_count"));
            Assert.That(coverage.MostMissingAttributes[0].ProductCount, Is.EqualTo(2));
            Assert.That(coverage.MostConflictedAttributes[0].AttributeKey, Is.EqualTo("screen_size_inch"));
            Assert.That(screenSize.ConflictProductCount, Is.EqualTo(1));
            Assert.That(screenSize.AgreementPercent, Is.EqualTo(50.00m));
            Assert.That(nativeResolution.CoveragePercent, Is.EqualTo(50.00m));
            Assert.That(nativeResolution.MissingProductCount, Is.EqualTo(1));
            Assert.That(sourceScores[0].SourceName, Is.EqualTo("alpha"));
            Assert.That(sourceScores[0].QualityScore, Is.GreaterThan(sourceScores[1].QualityScore));
            Assert.That(sourceScores[0].AgreementPercent, Is.EqualTo(100.00m));
            Assert.That(sourceScores[1].AgreementPercent, Is.EqualTo(66.67m));
        });
    }

    private static CanonicalAttributeValue CreateCanonicalAttribute(string key, object value, string valueType, decimal confidence, string? unit = null, bool hasConflict = false)
    {
        return new CanonicalAttributeValue
        {
            AttributeKey = key,
            Value = value,
            ValueType = valueType,
            Unit = unit,
            Confidence = confidence,
            HasConflict = hasConflict
        };
    }

    private static NormalisedAttributeValue CreateNormalisedAttribute(string key, object value, string valueType, decimal confidence, string? unit = null)
    {
        return new NormalisedAttributeValue
        {
            AttributeKey = key,
            Value = value,
            ValueType = valueType,
            Unit = unit,
            Confidence = confidence,
            SourceAttributeKey = key,
            OriginalValue = value.ToString()
        };
    }
}