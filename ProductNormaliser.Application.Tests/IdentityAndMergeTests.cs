using ProductNormaliser.Core.Merging;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Normalisation;

namespace ProductNormaliser.Tests;

public sealed class IdentityAndMergeTests
{
    [Test]
    public void Match_PrefersExactGtin()
    {
        var resolver = new ProductIdentityResolver();
        var incoming = CreateSourceProduct("source-1", "Samsung", "QE55S90D", "8806095563140", "Samsung QE55S90D OLED TV");
        var candidates = new[]
        {
            new CanonicalProduct
            {
                Id = "canonical-1",
                CategoryKey = "tv",
                Brand = "Samsung",
                ModelNumber = "QE55S90D",
                Gtin = "8806095563140",
                DisplayName = "Samsung QE55S90D"
            },
            new CanonicalProduct
            {
                Id = "canonical-2",
                CategoryKey = "tv",
                Brand = "Samsung",
                ModelNumber = "QE55S95D",
                Gtin = "1234567890123",
                DisplayName = "Samsung QE55S95D"
            }
        };

        var result = resolver.Match(incoming, candidates);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsMatch, Is.True);
            Assert.That(result.CanonicalProductId, Is.EqualTo("canonical-1"));
            Assert.That(result.Confidence, Is.EqualTo(1.00m));
            Assert.That(result.MatchReason, Is.EqualTo("Exact GTIN match."));
        });
    }

    [Test]
    public void Match_UsesExactBrandAndModelWhenGtinIsMissing()
    {
        var resolver = new ProductIdentityResolver();
        var incoming = CreateSourceProduct("source-1", "LG", "OLED55C4", null, "LG OLED55C4 55 Inch TV");
        var candidates = new[]
        {
            new CanonicalProduct
            {
                Id = "canonical-lg-c4",
                CategoryKey = "tv",
                Brand = "LG",
                ModelNumber = "OLED55C4",
                DisplayName = "LG OLED55C4"
            }
        };

        var result = resolver.Match(incoming, candidates);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsMatch, Is.True);
            Assert.That(result.CanonicalProductId, Is.EqualTo("canonical-lg-c4"));
            Assert.That(result.Confidence, Is.EqualTo(0.97m));
            Assert.That(result.MatchReason, Is.EqualTo("Exact brand and model number match."));
        });
    }

    [Test]
    public void Merge_WithAgreeingValues_IncreasesConfidenceAndRetainsEvidence()
    {
        var mergeService = new CanonicalMergeService();
        var existing = mergeService.Merge(null, CreateNormalisedTvSource(
            sourceId: "source-1",
            sourceName: "retailer-one",
            brand: "Samsung",
            model: "QE55S90D",
            gtin: "8806095563140",
            title: "Samsung QE55S90D OLED TV",
            screenSizeRaw: "55 inches"));

        var firstConfidence = existing.Attributes["screen_size_inch"].Confidence;

        var merged = mergeService.Merge(existing, CreateNormalisedTvSource(
            sourceId: "source-2",
            sourceName: "retailer-two",
            brand: "Samsung",
            model: "QE55S90D",
            gtin: "8806095563140",
            title: "Samsung QE55S90D OLED TV",
            screenSizeRaw: "140 cm"));

        var attribute = merged.Attributes["screen_size_inch"];

        Assert.Multiple(() =>
        {
            Assert.That(attribute.Value, Is.EqualTo(55m));
            Assert.That(attribute.HasConflict, Is.False);
            Assert.That(attribute.Confidence, Is.GreaterThan(firstConfidence));
            Assert.That(attribute.Evidence, Has.Count.EqualTo(2));
            Assert.That(attribute.Evidence.Select(evidence => evidence.SourceName), Is.EquivalentTo(new[] { "retailer-one", "retailer-two" }));
        });
    }

    [Test]
    public void Merge_WithConflictingValues_PreservesPreviousValueAndMarksConflict()
    {
        var mergeService = new CanonicalMergeService();
        var conflictDetector = new ConflictDetector();
        var existing = mergeService.Merge(null, CreateNormalisedTvSource(
            sourceId: "source-1",
            sourceName: "retailer-one",
            brand: "Samsung",
            model: "QE55S90D",
            gtin: "8806095563140",
            title: "Samsung QE55S90D OLED TV",
            screenSizeRaw: "55 inches"));

        var merged = mergeService.Merge(existing, CreateNormalisedTvSource(
            sourceId: "source-2",
            sourceName: "retailer-two",
            brand: "Samsung",
            model: "QE55S90D",
            gtin: "8806095563140",
            title: "Samsung QE55S90D OLED TV",
            screenSizeRaw: "65 inches"));

        var conflicts = conflictDetector.Detect(merged);

        Assert.Multiple(() =>
        {
            Assert.That(merged.Attributes["screen_size_inch"].Value, Is.EqualTo(65m));
            Assert.That(merged.Attributes["screen_size_inch"].HasConflict, Is.True);
            Assert.That(conflicts, Has.Count.EqualTo(1));
            Assert.That(conflicts[0].AttributeKey, Is.EqualTo("screen_size_inch"));
            Assert.That(conflicts[0].Reason, Is.EqualTo("Materially different numeric values detected across sources."));
            Assert.That(conflicts[0].SuggestedValue, Is.EqualTo(65m));
            Assert.That(conflicts[0].SuggestedSourceName, Is.EqualTo("retailer-two"));
            Assert.That(conflicts[0].HighestConfidenceValue, Is.EqualTo(65m));
        });
    }

    [Test]
    public void Merge_UsesWeightedSelectionToPreferTrustedRecentValue()
    {
        var mergeService = new CanonicalMergeService();
        var existing = mergeService.Merge(null, CreateWeightedSource(
            sourceId: "source-1",
            sourceName: "low-trust-source",
            screenSize: 55m,
            attributeConfidence: 0.62m,
            fetchedUtc: new DateTime(2026, 01, 01, 10, 00, 00, DateTimeKind.Utc),
            attributeCount: 1));

        var merged = mergeService.Merge(existing, CreateWeightedSource(
            sourceId: "source-2",
            sourceName: "high-trust-source",
            screenSize: 65m,
            attributeConfidence: 0.98m,
            fetchedUtc: new DateTime(2026, 03, 20, 10, 00, 00, DateTimeKind.Utc),
            attributeCount: 5));

        var attribute = merged.Attributes["screen_size_inch"];

        Assert.Multiple(() =>
        {
            Assert.That(attribute.Value, Is.EqualTo(65m));
            Assert.That(attribute.WinningSourceName, Is.EqualTo("high-trust-source"));
            Assert.That(attribute.MergeWeight, Is.GreaterThan(0m));
            Assert.That(attribute.HasConflict, Is.True);
        });
    }

    [Test]
    public void Merge_UsesHistoricalTrustToBreakTies()
    {
        var mergeWeightCalculator = new MergeWeightCalculator(
            new StubSourceTrustService(new Dictionary<string, decimal>
            {
                ["trusted-source"] = 0.95m,
                ["weak-source"] = 0.35m
            }),
            new StubAttributeStabilityService(1.00m));
        var mergeService = new CanonicalMergeService(mergeWeightCalculator: mergeWeightCalculator);

        var existing = mergeService.Merge(null, CreateWeightedSource(
            sourceId: "source-1",
            sourceName: "weak-source",
            screenSize: 55m,
            attributeConfidence: 0.90m,
            fetchedUtc: new DateTime(2026, 03, 20, 10, 00, 00, DateTimeKind.Utc),
            attributeCount: 4));

        var merged = mergeService.Merge(existing, CreateWeightedSource(
            sourceId: "source-2",
            sourceName: "trusted-source",
            screenSize: 65m,
            attributeConfidence: 0.90m,
            fetchedUtc: new DateTime(2026, 03, 20, 10, 01, 00, DateTimeKind.Utc),
            attributeCount: 4));

        Assert.Multiple(() =>
        {
            Assert.That(merged.Attributes["screen_size_inch"].Value, Is.EqualTo(65m));
            Assert.That(merged.Attributes["screen_size_inch"].HistoricalTrustScore, Is.EqualTo(0.95m));
            Assert.That(merged.Attributes["screen_size_inch"].WinningSourceName, Is.EqualTo("trusted-source"));
        });
    }

    [Test]
    public void Merge_RetainsEvidenceTrailAndSourceLinks()
    {
        var mergeService = new CanonicalMergeService();
        var merged = mergeService.Merge(null, CreateNormalisedTvSource(
            sourceId: "source-1",
            sourceName: "retailer-one",
            brand: "LG",
            model: "OLED55C4",
            gtin: null,
            title: "LG OLED55C4 55 Inch TV",
            screenSizeRaw: "55 inches"));

        merged = mergeService.Merge(merged, CreateNormalisedTvSource(
            sourceId: "source-2",
            sourceName: "lg-manufacturer-store",
            brand: "LG",
            model: "OLED55C4",
            gtin: null,
            title: "LG OLED55C4 OLED evo TV",
            screenSizeRaw: "55 in"));

        Assert.Multiple(() =>
        {
            Assert.That(merged.Sources, Has.Count.EqualTo(2));
            Assert.That(merged.Attributes["screen_size_inch"].Evidence, Has.Count.EqualTo(2));
            Assert.That(merged.Attributes["screen_size_inch"].Evidence[1].SourceUrl, Is.EqualTo("https://example.com/source-2"));
        });
    }

    [Test]
    public void Match_UsesStrongTitleAndModelSimilarityWhenExactIdentifiersAreMissing()
    {
        var resolver = new ProductIdentityResolver();
        var incoming = CreateSourceProduct("source-1", "Sony", "KD55X75WL", null, "Sony Bravia KD-55X75WL 55 inch 4K TV");
        var candidates = new[]
        {
            new CanonicalProduct
            {
                Id = "canonical-sony",
                CategoryKey = "tv",
                Brand = "Sony",
                ModelNumber = "KD55X75WL EU",
                DisplayName = "Sony Bravia KD55X75WL 4K HDR TV"
            }
        };

        var result = resolver.Match(incoming, candidates);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsMatch, Is.True);
            Assert.That(result.CanonicalProductId, Is.EqualTo("canonical-sony"));
            Assert.That(result.Confidence, Is.GreaterThanOrEqualTo(0.86m));
            Assert.That(result.MatchReason, Does.StartWith("Strong title/model similarity"));
        });
    }

    private static SourceProduct CreateSourceProduct(string sourceId, string brand, string model, string? gtin, string title)
    {
        return new SourceProduct
        {
            Id = sourceId,
            SourceName = "test-source",
            SourceUrl = $"https://example.com/{sourceId}",
            CategoryKey = "tv",
            Brand = brand,
            ModelNumber = model,
            Gtin = gtin,
            Title = title,
            RawSchemaJson = "{}",
            FetchedUtc = new DateTime(2026, 03, 20, 11, 00, 00, DateTimeKind.Utc)
        };
    }

    private static SourceProduct CreateNormalisedTvSource(
        string sourceId,
        string sourceName,
        string brand,
        string model,
        string? gtin,
        string title,
        string screenSizeRaw)
    {
        var sourceProduct = new SourceProduct
        {
            Id = sourceId,
            SourceName = sourceName,
            SourceUrl = $"https://example.com/{sourceId}",
            CategoryKey = "tv",
            Brand = brand,
            ModelNumber = model,
            Gtin = gtin,
            Title = title,
            RawSchemaJson = "{}",
            FetchedUtc = new DateTime(2026, 03, 20, 11, 00, 00, DateTimeKind.Utc).AddMinutes(sourceId == "source-1" ? 0 : 10),
            RawAttributes = new Dictionary<string, SourceAttributeValue>
            {
                ["Screen Size"] = new()
                {
                    AttributeKey = "Screen Size",
                    Value = screenSizeRaw,
                    ValueType = "string"
                }
            }
        };

        sourceProduct.NormalisedAttributes = new TvAttributeNormaliser().Normalise(sourceProduct.CategoryKey, sourceProduct.RawAttributes);
        return sourceProduct;
    }

    private static SourceProduct CreateWeightedSource(
        string sourceId,
        string sourceName,
        decimal screenSize,
        decimal attributeConfidence,
        DateTime fetchedUtc,
        int attributeCount)
    {
        var sourceProduct = new SourceProduct
        {
            Id = sourceId,
            SourceName = sourceName,
            SourceUrl = $"https://example.com/{sourceId}",
            CategoryKey = "tv",
            Brand = "Sony",
            ModelNumber = "A95L",
            Title = "Sony A95L",
            RawSchemaJson = "{}",
            FetchedUtc = fetchedUtc,
            NormalisedAttributes = new Dictionary<string, NormalisedAttributeValue>
            {
                ["screen_size_inch"] = new()
                {
                    AttributeKey = "screen_size_inch",
                    Value = screenSize,
                    ValueType = "decimal",
                    Unit = "inch",
                    Confidence = attributeConfidence,
                    SourceAttributeKey = "Screen Size",
                    OriginalValue = $"{screenSize} inches"
                }
            }
        };

        for (var index = 0; index < attributeCount - 1; index++)
        {
            sourceProduct.NormalisedAttributes[$"extra_attribute_{index}"] = new NormalisedAttributeValue
            {
                AttributeKey = $"extra_attribute_{index}",
                Value = $"value-{index}",
                ValueType = "string",
                Confidence = attributeConfidence
            };
        }

        return sourceProduct;
    }

    private sealed class StubSourceTrustService(Dictionary<string, decimal> scores) : ProductNormaliser.Core.Interfaces.ISourceTrustService
    {
        public void CaptureSnapshot(string sourceName, string categoryKey)
        {
        }

        public decimal GetHistoricalTrustScore(string sourceName, string categoryKey)
            => scores.TryGetValue(sourceName, out var score) ? score : 0.72m;

        public IReadOnlyList<SourceQualitySnapshot> GetSourceHistory(string categoryKey, string? sourceName = null, int? timeRangeDays = null, int limit = 30) => [];
    }

    private sealed class StubAttributeStabilityService(decimal score) : ProductNormaliser.Core.Interfaces.IAttributeStabilityService
    {
        public decimal GetStabilityScore(string categoryKey, string attributeKey) => score;

        public IReadOnlyList<AttributeStabilityScore> GetScores(string categoryKey) => [];
    }
}