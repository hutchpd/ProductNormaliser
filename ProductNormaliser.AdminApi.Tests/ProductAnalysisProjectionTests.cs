using ProductNormaliser.AdminApi.Services;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Normalisation;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.AdminApi.Tests;

public sealed class ProductAnalysisProjectionTests
{
    [Test]
    public void BuildSummary_ComputesQualitySignals()
    {
        var summary = ProductAnalysisProjection.BuildSummary(CreateProduct(), CreateSchemaRegistry(), CreateAttributeRegistry());

        Assert.Multiple(() =>
        {
            Assert.That(summary.SourceCount, Is.EqualTo(2));
            Assert.That(summary.EvidenceCount, Is.EqualTo(3));
            Assert.That(summary.HasConflict, Is.True);
            Assert.That(summary.CompletenessStatus, Is.EqualTo("partial"));
            Assert.That(summary.FreshnessStatus, Is.EqualTo("stale"));
            Assert.That(summary.KeyAttributes.Select(attribute => attribute.AttributeKey), Does.Contain("brand"));
            Assert.That(summary.KeyAttributes.Select(attribute => attribute.AttributeKey), Does.Contain("model_number"));
        });
    }

    [Test]
    public void MatchesFilters_AppliesExplorerCriteria()
    {
        var summary = ProductAnalysisProjection.BuildSummary(CreateProduct(), CreateSchemaRegistry(), CreateAttributeRegistry());

        Assert.Multiple(() =>
        {
            Assert.That(ProductAnalysisProjection.MatchesFilters(summary, 2, "stale", "with_conflicts", "partial"), Is.True);
            Assert.That(ProductAnalysisProjection.MatchesFilters(summary, 3, "stale", "with_conflicts", "partial"), Is.False);
            Assert.That(ProductAnalysisProjection.MatchesFilters(summary, 2, "fresh", "with_conflicts", "partial"), Is.False);
            Assert.That(ProductAnalysisProjection.MatchesFilters(summary, 2, "stale", "without_conflicts", "partial"), Is.False);
        });
    }

    private static CanonicalProduct CreateProduct()
    {
        return new CanonicalProduct
        {
            Id = "canon-1",
            CategoryKey = "tv",
            Brand = "Sony",
            ModelNumber = "XR-55A80L",
            DisplayName = "Sony Bravia XR 55",
            UpdatedUtc = DateTime.UtcNow.AddDays(-42),
            Sources =
            [
                new ProductSourceLink { SourceName = "AO UK", SourceProductId = "ao-1", SourceUrl = "https://ao.example/1" },
                new ProductSourceLink { SourceName = "Currys", SourceProductId = "currys-1", SourceUrl = "https://currys.example/1" }
            ],
            Attributes = new Dictionary<string, CanonicalAttributeValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["brand"] = new() { AttributeKey = "brand", Value = "Sony", ValueType = "string", Confidence = 1m, Evidence = [new AttributeEvidence(), new AttributeEvidence()] },
                ["model_number"] = new() { AttributeKey = "model_number", Value = "XR-55A80L", ValueType = "string", Confidence = 1m },
                ["panel_type"] = new() { AttributeKey = "panel_type", Value = "OLED", ValueType = "string", Confidence = 0.95m, Evidence = [new AttributeEvidence()] },
                ["screen_size_inch"] = new() { AttributeKey = "screen_size_inch", Value = 55, ValueType = "number", Unit = "in", Confidence = 0.9m, HasConflict = true }
            }
        };
    }

    private static CategorySchemaRegistry CreateSchemaRegistry()
    {
        return new CategorySchemaRegistry([
            new TvCategorySchemaProvider(),
            new MonitorCategorySchemaProvider(),
            new LaptopCategorySchemaProvider(),
            new RefrigeratorCategorySchemaProvider()
        ]);
    }

    private static CategoryAttributeNormaliserRegistry CreateAttributeRegistry()
    {
        return new CategoryAttributeNormaliserRegistry([
            new TvAttributeNormaliser(),
            new MonitorAttributeNormaliser(),
            new LaptopAttributeNormaliser(),
            new RefrigeratorAttributeNormaliser()
        ]);
    }
}