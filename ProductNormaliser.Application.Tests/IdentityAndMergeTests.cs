using ProductNormaliser.Core.Merging;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Normalisation;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.IdentityAndMerge)]
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

    [Test]
    public void Match_SmartphoneExactBrandAndModel_WithAlignedVariantSignals_Succeeds()
    {
        var resolver = new ProductIdentityResolver();
        var incoming = CreateSmartphoneSourceProduct(
            sourceId: "phone-source-1",
            brand: "Samsung",
            modelNumber: "SM-S928B",
            title: "Samsung Galaxy S24 Ultra 256GB",
            attributes:
            [
                ("model_family", "Galaxy S24 Ultra", "string"),
                ("storage_capacity_gb", 256, "integer"),
                ("regional_variant", "EU", "string"),
                ("carrier_lock_status", "Unlocked", "string")
            ]);
        var candidates = new[]
        {
            CreateSmartphoneCandidate(
                id: "canon-phone-1",
                brand: "Samsung",
                modelNumber: "SM-S928B",
                displayName: "Samsung Galaxy S24 Ultra",
                attributes:
                [
                    ("model_family", "Galaxy S24 Ultra", "string"),
                    ("storage_capacity_gb", 256, "integer"),
                    ("regional_variant", "EU", "string"),
                    ("carrier_lock_status", "Unlocked", "string")
                ])
        };

        var result = resolver.Match(incoming, candidates);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsMatch, Is.True);
            Assert.That(result.CanonicalProductId, Is.EqualTo("canon-phone-1"));
            Assert.That(result.MatchReason, Is.EqualTo("Exact brand and model number match."));
        });
    }

    [Test]
    public void Match_SmartphoneSameModelDifferentStorage_DoesNotMerge()
    {
        var resolver = new ProductIdentityResolver();
        var incoming = CreateSmartphoneSourceProduct(
            sourceId: "phone-source-2",
            brand: "Samsung",
            modelNumber: "SM-S928B",
            title: "Samsung Galaxy S24 Ultra 256GB",
            attributes:
            [
                ("model_family", "Galaxy S24 Ultra", "string"),
                ("storage_capacity_gb", 256, "integer")
            ]);
        var candidates = new[]
        {
            CreateSmartphoneCandidate(
                id: "canon-phone-storage-conflict",
                brand: "Samsung",
                modelNumber: "SM-S928B",
                displayName: "Samsung Galaxy S24 Ultra 512GB",
                attributes:
                [
                    ("model_family", "Galaxy S24 Ultra", "string"),
                    ("storage_capacity_gb", 512, "integer")
                ])
        };

        var result = resolver.Match(incoming, candidates);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsMatch, Is.False);
            Assert.That(result.MatchReason, Is.EqualTo("Strong smartphone variant conflict prevented a safe match."));
        });
    }

    [Test]
    public void Match_SmartphoneSameModelDifferentRegionalVariant_DoesNotMerge()
    {
        var resolver = new ProductIdentityResolver();
        var incoming = CreateSmartphoneSourceProduct(
            sourceId: "phone-source-3",
            brand: "Samsung",
            modelNumber: "SM-S928B",
            title: "Samsung Galaxy S24 Ultra EU",
            attributes:
            [
                ("model_family", "Galaxy S24 Ultra", "string"),
                ("regional_variant", "EU", "string")
            ]);
        var candidates = new[]
        {
            CreateSmartphoneCandidate(
                id: "canon-phone-region-conflict",
                brand: "Samsung",
                modelNumber: "SM-S928B",
                displayName: "Samsung Galaxy S24 Ultra US",
                attributes:
                [
                    ("model_family", "Galaxy S24 Ultra", "string"),
                    ("regional_variant", "US", "string")
                ])
        };

        var result = resolver.Match(incoming, candidates);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsMatch, Is.False);
            Assert.That(result.MatchReason, Is.EqualTo("Strong smartphone variant conflict prevented a safe match."));
        });
    }

    [Test]
    public void Match_SmartphoneSameModelDifferentCarrierLockStatus_DoesNotMerge()
    {
        var resolver = new ProductIdentityResolver();
        var incoming = CreateSmartphoneSourceProduct(
            sourceId: "phone-source-4",
            brand: "Apple",
            modelNumber: "A3090",
            title: "Apple iPhone 15 Unlocked",
            attributes:
            [
                ("model_family", "iPhone 15", "string"),
                ("carrier_lock_status", "Unlocked", "string")
            ]);
        var candidates = new[]
        {
            CreateSmartphoneCandidate(
                id: "canon-phone-lock-conflict",
                brand: "Apple",
                modelNumber: "A3090",
                displayName: "Apple iPhone 15 Carrier Locked",
                attributes:
                [
                    ("model_family", "iPhone 15", "string"),
                    ("carrier_lock_status", "Carrier Locked", "string")
                ])
        };

        var result = resolver.Match(incoming, candidates);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsMatch, Is.False);
            Assert.That(result.MatchReason, Is.EqualTo("Strong smartphone variant conflict prevented a safe match."));
        });
    }

    [Test]
    public void Match_SmartphoneExactManufacturerPartNumber_StillSucceedsDespiteVariantNoise()
    {
        var resolver = new ProductIdentityResolver();
        var incoming = CreateSmartphoneSourceProduct(
            sourceId: "phone-source-5",
            brand: "Samsung",
            modelNumber: "SM-S928B",
            title: "Samsung Galaxy S24 Ultra 256GB",
            attributes:
            [
                ("manufacturer_part_number", "SM-S928BZKDEUB", "string"),
                ("storage_capacity_gb", 256, "integer"),
                ("regional_variant", "EU", "string")
            ]);
        var candidates = new[]
        {
            CreateSmartphoneCandidate(
                id: "canon-phone-mpn",
                brand: "Samsung",
                modelNumber: "SM-S928B",
                displayName: "Samsung Galaxy S24 Ultra",
                attributes:
                [
                    ("manufacturer_part_number", "SM-S928BZKDEUB", "string"),
                    ("storage_capacity_gb", 512, "integer"),
                    ("regional_variant", "EU", "string")
                ])
        };

        var result = resolver.Match(incoming, candidates);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsMatch, Is.True);
            Assert.That(result.CanonicalProductId, Is.EqualTo("canon-phone-mpn"));
            Assert.That(result.MatchReason, Is.EqualTo("Exact manufacturer part number match."));
            Assert.That(result.Confidence, Is.EqualTo(0.98m));
        });
    }

    [Test]
    public void Match_TabletSameModelDifferentConnectivity_DoesNotMerge()
    {
        var resolver = new ProductIdentityResolver();
        var incoming = CreateTabletSourceProduct(
            sourceId: "tablet-source-1",
            brand: "Samsung",
            modelNumber: "SM-X610",
            title: "Samsung Galaxy Tab S9 FE Wi-Fi",
            attributes:
            [
                ("model_family", "Galaxy Tab S9 FE", "string"),
                ("connectivity", "Wi-Fi", "string")
            ]);
        var candidates = new[]
        {
            CreateTabletCandidate(
                id: "canon-tablet-connectivity-conflict",
                brand: "Samsung",
                modelNumber: "SM-X610",
                displayName: "Samsung Galaxy Tab S9 FE Wi-Fi + Cellular",
                attributes:
                [
                    ("model_family", "Galaxy Tab S9 FE", "string"),
                    ("connectivity", "Wi-Fi + Cellular", "string")
                ])
        };

        var result = resolver.Match(incoming, candidates);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsMatch, Is.False);
            Assert.That(result.MatchReason, Is.EqualTo("Strong tablet variant conflict prevented a safe match."));
        });
    }

    [Test]
    public void Match_TabletSameModelDifferentCellularGeneration_DoesNotMerge()
    {
        var resolver = new ProductIdentityResolver();
        var incoming = CreateTabletSourceProduct(
            sourceId: "tablet-source-2",
            brand: "Samsung",
            modelNumber: "SM-X616",
            title: "Samsung Galaxy Tab S9 FE 5G",
            attributes:
            [
                ("model_family", "Galaxy Tab S9 FE", "string"),
                ("connectivity", "Wi-Fi + Cellular", "string"),
                ("cellular_generation", "5G", "string")
            ]);
        var candidates = new[]
        {
            CreateTabletCandidate(
                id: "canon-tablet-generation-conflict",
                brand: "Samsung",
                modelNumber: "SM-X616",
                displayName: "Samsung Galaxy Tab S9 FE LTE",
                attributes:
                [
                    ("model_family", "Galaxy Tab S9 FE", "string"),
                    ("connectivity", "Wi-Fi + Cellular", "string"),
                    ("cellular_generation", "4G", "string")
                ])
        };

        var result = resolver.Match(incoming, candidates);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsMatch, Is.False);
            Assert.That(result.MatchReason, Is.EqualTo("Strong tablet variant conflict prevented a safe match."));
        });
    }

    [Test]
    public void Match_TabletExactManufacturerPartNumber_StillSucceedsDespiteConnectivityNoise()
    {
        var resolver = new ProductIdentityResolver();
        var incoming = CreateTabletSourceProduct(
            sourceId: "tablet-source-3",
            brand: "Samsung",
            modelNumber: "SM-X610",
            title: "Samsung Galaxy Tab S9 FE 128GB",
            attributes:
            [
                ("manufacturer_part_number", "SM-X610NZAEEUB", "string"),
                ("connectivity", "Wi-Fi", "string"),
                ("storage_capacity_gb", 128, "integer")
            ]);
        var candidates = new[]
        {
            CreateTabletCandidate(
                id: "canon-tablet-mpn",
                brand: "Samsung",
                modelNumber: "SM-X610",
                displayName: "Samsung Galaxy Tab S9 FE",
                attributes:
                [
                    ("manufacturer_part_number", "SM-X610NZAEEUB", "string"),
                    ("connectivity", "Wi-Fi + Cellular", "string"),
                    ("storage_capacity_gb", 256, "integer")
                ])
        };

        var result = resolver.Match(incoming, candidates);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsMatch, Is.True);
            Assert.That(result.CanonicalProductId, Is.EqualTo("canon-tablet-mpn"));
            Assert.That(result.MatchReason, Is.EqualTo("Exact manufacturer part number match."));
        });
    }

    [Test]
    public void Match_HeadphonesSameModelDifferentConnectionType_DoesNotMerge()
    {
        var resolver = new ProductIdentityResolver();
        var incoming = CreateHeadphonesSourceProduct(
            sourceId: "headphones-source-1",
            brand: "Sony",
            modelNumber: "WH-CH520",
            title: "Sony WH-CH520 Bluetooth Headphones",
            attributes:
            [
                ("model_family", "WH-CH520", "string"),
                ("connection_type", "Bluetooth", "string")
            ]);
        var candidates = new[]
        {
            CreateHeadphonesCandidate(
                id: "canon-headphones-connection-conflict",
                brand: "Sony",
                modelNumber: "WH-CH520",
                displayName: "Sony WH-CH520 Wired",
                attributes:
                [
                    ("model_family", "WH-CH520", "string"),
                    ("connection_type", "Wired", "string")
                ])
        };

        var result = resolver.Match(incoming, candidates);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsMatch, Is.False);
            Assert.That(result.MatchReason, Is.EqualTo("Strong headphones variant conflict prevented a safe match."));
        });
    }

    [Test]
    public void Match_SpeakersSameModelDifferentConnectionType_DoesNotMerge()
    {
        var resolver = new ProductIdentityResolver();
        var incoming = CreateSpeakersSourceProduct(
            sourceId: "speakers-source-1",
            brand: "Sonos",
            modelNumber: "Era 100",
            title: "Sonos Era 100 Bluetooth Speaker",
            attributes:
            [
                ("model_family", "Era 100", "string"),
                ("connection_type", "Bluetooth", "string")
            ]);
        var candidates = new[]
        {
            CreateSpeakersCandidate(
                id: "canon-speakers-connection-conflict",
                brand: "Sonos",
                modelNumber: "Era 100",
                displayName: "Sonos Era 100 Wi-Fi Speaker",
                attributes:
                [
                    ("model_family", "Era 100", "string"),
                    ("connection_type", "Wi-Fi", "string")
                ])
        };

        var result = resolver.Match(incoming, candidates);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsMatch, Is.False);
            Assert.That(result.MatchReason, Is.EqualTo("Strong speakers variant conflict prevented a safe match."));
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

    private static SourceProduct CreateSmartphoneSourceProduct(
        string sourceId,
        string brand,
        string? modelNumber,
        string title,
        params (string Key, object Value, string ValueType)[] attributes)
    {
        return CreateCategorySourceProduct("smartphone", sourceId, brand, modelNumber, title, attributes);
    }

    private static SourceProduct CreateTabletSourceProduct(
        string sourceId,
        string brand,
        string? modelNumber,
        string title,
        params (string Key, object Value, string ValueType)[] attributes)
    {
        return CreateCategorySourceProduct("tablet", sourceId, brand, modelNumber, title, attributes);
    }

    private static SourceProduct CreateHeadphonesSourceProduct(
        string sourceId,
        string brand,
        string? modelNumber,
        string title,
        params (string Key, object Value, string ValueType)[] attributes)
    {
        return CreateCategorySourceProduct("headphones", sourceId, brand, modelNumber, title, attributes);
    }

    private static SourceProduct CreateSpeakersSourceProduct(
        string sourceId,
        string brand,
        string? modelNumber,
        string title,
        params (string Key, object Value, string ValueType)[] attributes)
    {
        return CreateCategorySourceProduct("speakers", sourceId, brand, modelNumber, title, attributes);
    }

    private static SourceProduct CreateCategorySourceProduct(
        string categoryKey,
        string sourceId,
        string brand,
        string? modelNumber,
        string title,
        params (string Key, object Value, string ValueType)[] attributes)
    {
        return new SourceProduct
        {
            Id = sourceId,
            SourceName = "test-source",
            SourceUrl = $"https://example.com/{sourceId}",
            CategoryKey = categoryKey,
            Brand = brand,
            ModelNumber = modelNumber,
            Title = title,
            RawSchemaJson = "{}",
            FetchedUtc = new DateTime(2026, 03, 20, 11, 00, 00, DateTimeKind.Utc),
            NormalisedAttributes = attributes.ToDictionary(
                attribute => attribute.Key,
                attribute => new NormalisedAttributeValue
                {
                    AttributeKey = attribute.Key,
                    Value = attribute.Value,
                    ValueType = attribute.ValueType,
                    Confidence = 0.95m,
                    SourceAttributeKey = attribute.Key,
                    OriginalValue = attribute.Value.ToString()
                },
                StringComparer.OrdinalIgnoreCase)
        };
    }

    private static CanonicalProduct CreateSmartphoneCandidate(
        string id,
        string brand,
        string? modelNumber,
        string displayName,
        params (string Key, object Value, string ValueType)[] attributes)
    {
        return CreateCategoryCandidate("smartphone", id, brand, modelNumber, displayName, attributes);
    }

    private static CanonicalProduct CreateTabletCandidate(
        string id,
        string brand,
        string? modelNumber,
        string displayName,
        params (string Key, object Value, string ValueType)[] attributes)
    {
        return CreateCategoryCandidate("tablet", id, brand, modelNumber, displayName, attributes);
    }

    private static CanonicalProduct CreateHeadphonesCandidate(
        string id,
        string brand,
        string? modelNumber,
        string displayName,
        params (string Key, object Value, string ValueType)[] attributes)
    {
        return CreateCategoryCandidate("headphones", id, brand, modelNumber, displayName, attributes);
    }

    private static CanonicalProduct CreateSpeakersCandidate(
        string id,
        string brand,
        string? modelNumber,
        string displayName,
        params (string Key, object Value, string ValueType)[] attributes)
    {
        return CreateCategoryCandidate("speakers", id, brand, modelNumber, displayName, attributes);
    }

    private static CanonicalProduct CreateCategoryCandidate(
        string categoryKey,
        string id,
        string brand,
        string? modelNumber,
        string displayName,
        params (string Key, object Value, string ValueType)[] attributes)
    {
        return new CanonicalProduct
        {
            Id = id,
            CategoryKey = categoryKey,
            Brand = brand,
            ModelNumber = modelNumber,
            DisplayName = displayName,
            Attributes = attributes.ToDictionary(
                attribute => attribute.Key,
                attribute => new CanonicalAttributeValue
                {
                    AttributeKey = attribute.Key,
                    Value = attribute.Value,
                    ValueType = attribute.ValueType
                },
                StringComparer.OrdinalIgnoreCase)
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