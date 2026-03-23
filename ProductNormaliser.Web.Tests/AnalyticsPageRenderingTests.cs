using Microsoft.AspNetCore.Mvc.Testing;
using ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Tests;

[TestFixture]
public sealed class AnalyticsPageRenderingTests
{
    [Test]
    public async Task QualityDashboardPage_RendersCategoryScopedAnalytics()
    {
        var fakeAdminApiClient = new FakeAdminApiClient
        {
            Categories =
            [
                new CategoryMetadataDto { CategoryKey = "monitor", DisplayName = "Monitors", IsEnabled = true, CrawlSupportStatus = "Supported" }
            ],
            DetailedCoverage = new DetailedCoverageResponseDto
            {
                CategoryKey = "monitor",
                TotalCanonicalProducts = 12,
                TotalSourceProducts = 28,
                Attributes =
                [
                    new AttributeCoverageDetailDto
                    {
                        AttributeKey = "refresh_rate_hz",
                        DisplayName = "Refresh Rate",
                        CoveragePercent = 62m,
                        ReliabilityScore = 58m,
                        ConflictPercent = 21m,
                        AgreementPercent = 79m,
                        PresentProductCount = 8,
                        MissingProductCount = 4,
                        AverageConfidence = 0.86m
                    }
                ]
            },
            UnmappedAttributes =
            [
                new UnmappedAttributeDto
                {
                    CanonicalKey = "panel_type",
                    RawAttributeKey = "display_panel",
                    OccurrenceCount = 5,
                    SourceNames = ["Northwind"],
                    SampleValues = ["IPS"]
                }
            ],
            SourceDisagreements =
            [
                new SourceAttributeDisagreementDto
                {
                    CategoryKey = "monitor",
                    SourceName = "Northwind",
                    AttributeKey = "refresh_rate_hz",
                    TotalComparisons = 8,
                    TimesDisagreed = 3,
                    DisagreementRate = 37.5m,
                    WinRate = 62.5m,
                    LastUpdatedUtc = new DateTime(2026, 3, 23, 9, 0, 0, DateTimeKind.Utc)
                }
            ],
            AttributeStability =
            [
                new AttributeStabilityDto
                {
                    CategoryKey = "monitor",
                    AttributeKey = "refresh_rate_hz",
                    ChangeCount = 4,
                    OscillationCount = 1,
                    DistinctValueCount = 3,
                    StabilityScore = 54m,
                    IsSuspicious = true,
                    SuspicionReason = "Supplier values keep flipping."
                }
            ]
        };

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var html = await client.GetStringAsync("/Quality?category=monitor");

        Assert.That(html, Does.Contain("Monitors quality dashboard"));
        Assert.That(html, Does.Contain("Coverage Heatmap"));
        Assert.That(html, Does.Contain("Unmapped attribute backlog"));
        Assert.That(html, Does.Contain("Source by attribute disagreement"));
        Assert.That(html, Does.Contain("refresh_rate_hz"));
        Assert.That(html, Does.Contain("selected=\"selected\">Monitors"));
    }

    [Test]
    public async Task SourceIntelligencePage_RendersTrustAndHotspots()
    {
        var fakeAdminApiClient = new FakeAdminApiClient
        {
            Categories =
            [
                new CategoryMetadataDto { CategoryKey = "monitor", DisplayName = "Monitors", IsEnabled = true, CrawlSupportStatus = "Supported" }
            ],
            Sources =
            [
                new SourceDto { SourceId = "northwind", DisplayName = "Northwind", SupportedCategoryKeys = ["monitor"] }
            ],
            SourceQualityScores =
            [
                new SourceQualityScoreDto
                {
                    SourceName = "Northwind",
                    SourceProductCount = 14,
                    CoveragePercent = 88m,
                    AgreementPercent = 84m,
                    AverageMappedAttributes = 9.2m,
                    AverageAttributeConfidence = 91m,
                    QualityScore = 87m
                }
            ],
            SourceHistory =
            [
                new SourceQualitySnapshotDto
                {
                    SourceName = "Northwind",
                    CategoryKey = "monitor",
                    TimestampUtc = new DateTime(2026, 3, 22, 8, 0, 0, DateTimeKind.Utc),
                    AttributeCoverage = 84m,
                    AgreementRate = 80m,
                    ConflictRate = 20m,
                    SuccessfulCrawlRate = 93m,
                    SpecStabilityScore = 76m,
                    HistoricalTrustScore = 81m
                }
            ],
            SourceDisagreements =
            [
                new SourceAttributeDisagreementDto
                {
                    SourceName = "Northwind",
                    CategoryKey = "monitor",
                    AttributeKey = "refresh_rate_hz",
                    TotalComparisons = 10,
                    TimesDisagreed = 2,
                    WinRate = 80m,
                    DisagreementRate = 20m,
                    LastUpdatedUtc = new DateTime(2026, 3, 23, 10, 0, 0, DateTimeKind.Utc)
                }
            ]
        };

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var html = await client.GetStringAsync("/Sources/Intelligence?category=monitor&source=Northwind");

        Assert.That(html, Does.Contain("Monitors source intelligence"));
        Assert.That(html, Does.Contain("Quality and trust overview"));
        Assert.That(html, Does.Contain("Trust Over Time"));
        Assert.That(html, Does.Contain("Disagreement hotspots"));
        Assert.That(html, Does.Contain("Northwind"));
        Assert.That(html, Does.Contain("selected=\"selected\">Northwind"));
    }
}
