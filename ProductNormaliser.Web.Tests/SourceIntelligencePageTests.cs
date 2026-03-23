using Microsoft.Extensions.Logging.Abstractions;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Tests;

public sealed class SourceIntelligencePageTests
{
    [Test]
    public async Task SourceIntelligence_OnGetAsync_BindsCategoryAndSourceSelection()
    {
        var client = new FakeAdminApiClient
        {
            Categories =
            [
                new CategoryMetadataDto { CategoryKey = "monitor", DisplayName = "Monitors", IsEnabled = true, CrawlSupportStatus = "Supported" }
            ],
            Sources =
            [
                new SourceDto { SourceId = "northwind", DisplayName = "Northwind", SupportedCategoryKeys = ["monitor"] },
                new SourceDto { SourceId = "contoso", DisplayName = "Contoso", SupportedCategoryKeys = ["monitor"] }
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
                },
                new SourceQualitySnapshotDto
                {
                    SourceName = "Contoso",
                    CategoryKey = "monitor",
                    TimestampUtc = new DateTime(2026, 3, 21, 8, 0, 0, DateTimeKind.Utc),
                    AttributeCoverage = 68m,
                    AgreementRate = 74m,
                    ConflictRate = 26m,
                    SuccessfulCrawlRate = 91m,
                    SpecStabilityScore = 63m,
                    HistoricalTrustScore = 72m
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
                },
                new SourceAttributeDisagreementDto
                {
                    SourceName = "Contoso",
                    CategoryKey = "monitor",
                    AttributeKey = "panel_type",
                    TotalComparisons = 7,
                    TimesDisagreed = 3,
                    WinRate = 57m,
                    DisagreementRate = 43m,
                    LastUpdatedUtc = new DateTime(2026, 3, 23, 9, 0, 0, DateTimeKind.Utc)
                }
            ]
        };

        var model = new ProductNormaliser.Web.Pages.Sources.IntelligenceModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.IntelligenceModel>.Instance)
        {
            CategoryKey = "monitor",
            SourceName = "Northwind",
            TimeRangeDays = 90
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(client.LastSourceQualityCategoryKey, Is.EqualTo("monitor"));
            Assert.That(client.LastSourceHistoryCategoryKey, Is.EqualTo("monitor"));
            Assert.That(client.LastSourceHistorySourceName, Is.Null);
            Assert.That(client.LastSourceHistoryTimeRangeDays, Is.EqualTo(90));
            Assert.That(client.LastSourceDisagreementsCategoryKey, Is.EqualTo("monitor"));
            Assert.That(client.LastSourceDisagreementsSourceName, Is.Null);
            Assert.That(client.LastSourceDisagreementsTimeRangeDays, Is.EqualTo(90));
            Assert.That(model.EffectiveSourceName, Is.EqualTo("Northwind"));
            Assert.That(model.TimeRangeDays, Is.EqualTo(90));
            Assert.That(model.TrendSnapshots, Has.Count.EqualTo(1));
            Assert.That(model.DisagreementHotspots, Has.Count.EqualTo(2));
            Assert.That(model.SourceMetrics, Has.Count.EqualTo(2));
            Assert.That(model.HighValueSources[0].SourceName, Is.EqualTo("Northwind"));
            Assert.That(model.WeakSources[0].SourceName, Is.EqualTo("Contoso"));
            Assert.That(model.SupportMatrixRows, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task SourceIntelligence_OnGetAsync_CategorySwitchingRefreshesTopSourceAndWindow()
    {
        var client = new FakeAdminApiClient
        {
            Categories =
            [
                new CategoryMetadataDto { CategoryKey = "tv", DisplayName = "TVs", IsEnabled = true, CrawlSupportStatus = "Supported" },
                new CategoryMetadataDto { CategoryKey = "monitor", DisplayName = "Monitors", IsEnabled = true, CrawlSupportStatus = "Supported" }
            ],
            Sources =
            [
                new SourceDto { SourceId = "northwind", DisplayName = "Northwind", SupportedCategoryKeys = ["tv"] },
                new SourceDto { SourceId = "contoso", DisplayName = "Contoso", SupportedCategoryKeys = ["monitor"] }
            ],
            SourceQualityScores =
            [
                new SourceQualityScoreDto
                {
                    SourceName = "Contoso",
                    SourceProductCount = 12,
                    CoveragePercent = 90m,
                    AgreementPercent = 89m,
                    AverageMappedAttributes = 10m,
                    AverageAttributeConfidence = 94m,
                    QualityScore = 91m
                }
            ],
            SourceHistory =
            [
                new SourceQualitySnapshotDto
                {
                    SourceName = "Contoso",
                    CategoryKey = "monitor",
                    TimestampUtc = new DateTime(2026, 3, 23, 10, 0, 0, DateTimeKind.Utc),
                    AttributeCoverage = 90m,
                    AgreementRate = 89m,
                    ConflictRate = 11m,
                    SuccessfulCrawlRate = 95m,
                    SpecStabilityScore = 82m,
                    HistoricalTrustScore = 92m
                }
            ]
        };

        var model = new ProductNormaliser.Web.Pages.Sources.IntelligenceModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.IntelligenceModel>.Instance)
        {
            CategoryKey = "monitor",
            TimeRangeDays = 7
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(client.LastSourceQualityCategoryKey, Is.EqualTo("monitor"));
            Assert.That(model.EffectiveSourceName, Is.EqualTo("Contoso"));
            Assert.That(model.TimeRangeLabel, Is.EqualTo("Last 7 days"));
            Assert.That(model.Hero.Title, Is.EqualTo("Monitors source intelligence"));
        });
    }

    [Test]
    public async Task SourceIntelligence_OnGetAsync_WithoutCategory_DefaultsToFirstRolloutCategory()
    {
        var client = new FakeAdminApiClient
        {
            Categories = [new CategoryMetadataDto { CategoryKey = "tv", DisplayName = "TVs", IsEnabled = true, CrawlSupportStatus = "Supported" }],
            Sources = [new SourceDto { SourceId = "northwind", DisplayName = "Northwind" }]
        };

        var model = new ProductNormaliser.Web.Pages.Sources.IntelligenceModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.IntelligenceModel>.Instance);

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(model.IsAwaitingSelection, Is.False);
            Assert.That(model.CategoryKey, Is.EqualTo("tv"));
            Assert.That(client.LastSourceQualityCategoryKey, Is.EqualTo("tv"));
        });
    }

    [Test]
    public async Task SourceIntelligence_OnGetAsync_WithEmptyAnalytics_ShowsEmptyState()
    {
        var client = new FakeAdminApiClient
        {
            Categories = [new CategoryMetadataDto { CategoryKey = "tv", DisplayName = "TVs", IsEnabled = true, CrawlSupportStatus = "Supported" }],
            Sources = [new SourceDto { SourceId = "northwind", DisplayName = "Northwind", SupportedCategoryKeys = ["tv"] }]
        };

        var model = new ProductNormaliser.Web.Pages.Sources.IntelligenceModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.IntelligenceModel>.Instance)
        {
            CategoryKey = "tv"
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.That(model.IsEmpty, Is.True);
    }

    [Test]
    public async Task SourceIntelligence_OnGetAsync_WhenAnalyticsFail_SetsErrorState()
    {
        var client = new FakeAdminApiClient
        {
            Categories = [new CategoryMetadataDto { CategoryKey = "tv", DisplayName = "TVs", IsEnabled = true, CrawlSupportStatus = "Supported" }],
            Sources = [new SourceDto { SourceId = "northwind", DisplayName = "Northwind", SupportedCategoryKeys = ["tv"] }],
            AnalyticsException = new AdminApiException("Source analytics are unavailable.")
        };

        var model = new ProductNormaliser.Web.Pages.Sources.IntelligenceModel(client, NullLogger<ProductNormaliser.Web.Pages.Sources.IntelligenceModel>.Instance)
        {
            CategoryKey = "tv"
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(model.ErrorMessage, Is.EqualTo("Source analytics are unavailable."));
            Assert.That(model.SourceQualityScores, Is.Empty);
            Assert.That(model.SourceHistory, Is.Empty);
            Assert.That(model.SourceMetrics, Is.Empty);
        });
    }
}
