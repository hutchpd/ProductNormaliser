using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Models;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Tests;

public sealed class QualityDashboardPageTests
{
    [Test]
    public async Task QualityDashboard_OnGetAsync_UsesSelectedCategoryAcrossAnalyticsCalls()
    {
        var client = new FakeAdminApiClient
        {
            Categories =
            [
                new CategoryMetadataDto { CategoryKey = "tv", DisplayName = "TVs", IsEnabled = true, CrawlSupportStatus = "Supported" },
                new CategoryMetadataDto { CategoryKey = "monitor", DisplayName = "Monitors", IsEnabled = true, CrawlSupportStatus = "Supported" }
            ],
            DetailedCoverage = new DetailedCoverageResponseDto
            {
                CategoryKey = "monitor",
                TotalCanonicalProducts = 12,
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

        var model = new ProductNormaliser.Web.Pages.Quality.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Quality.IndexModel>.Instance)
        {
            CategoryKey = "monitor"
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(client.LastCoverageCategoryKey, Is.EqualTo("monitor"));
            Assert.That(client.LastUnmappedCategoryKey, Is.EqualTo("monitor"));
            Assert.That(client.LastSourceDisagreementsCategoryKey, Is.EqualTo("monitor"));
            Assert.That(client.LastAttributeStabilityCategoryKey, Is.EqualTo("monitor"));
            Assert.That(model.CoverageHeatmap, Has.Count.EqualTo(1));
            Assert.That(model.DisagreementMatrix.Rows, Has.Count.EqualTo(1));
            Assert.That(model.StabilityChart[0].IsSuspicious, Is.True);
        });
    }

    [Test]
    public async Task QualityDashboard_OnGetAsync_WithoutCategory_DefaultsToFirstRolloutCategory()
    {
        var client = new FakeAdminApiClient
        {
            Categories = [new CategoryMetadataDto { CategoryKey = "tv", DisplayName = "TVs", IsEnabled = true, CrawlSupportStatus = "Supported" }]
        };

        var model = new ProductNormaliser.Web.Pages.Quality.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Quality.IndexModel>.Instance);

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(model.IsAwaitingSelection, Is.False);
            Assert.That(model.CategoryKey, Is.EqualTo("tv"));
            Assert.That(client.LastCoverageCategoryKey, Is.EqualTo("tv"));
            Assert.That(model.ErrorMessage, Is.Null);
        });
    }

    [Test]
    public async Task QualityDashboard_OnGetAsync_WithEmptyAnalytics_ShowsEmptyState()
    {
        var client = new FakeAdminApiClient
        {
            Categories = [new CategoryMetadataDto { CategoryKey = "tv", DisplayName = "TVs", IsEnabled = true, CrawlSupportStatus = "Supported" }],
            DetailedCoverage = new DetailedCoverageResponseDto { CategoryKey = "tv" }
        };

        var model = new ProductNormaliser.Web.Pages.Quality.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Quality.IndexModel>.Instance)
        {
            CategoryKey = "tv"
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.That(model.IsEmpty, Is.True);
    }

    [Test]
    public async Task QualityDashboard_OnGetAsync_WhenAnalyticsFail_SetsErrorState()
    {
        var client = new FakeAdminApiClient
        {
            Categories = [new CategoryMetadataDto { CategoryKey = "tv", DisplayName = "TVs", IsEnabled = true, CrawlSupportStatus = "Supported" }],
            AnalyticsException = new AdminApiException("Quality analytics are unavailable.")
        };

        var model = new ProductNormaliser.Web.Pages.Quality.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Quality.IndexModel>.Instance)
        {
            CategoryKey = "tv"
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(model.ErrorMessage, Is.EqualTo("Quality analytics are unavailable."));
            Assert.That(model.Coverage.Attributes, Is.Empty);
            Assert.That(model.UnmappedAttributes, Is.Empty);
        });
    }

    [Test]
    public async Task QualityDashboard_OnPostSaveViewAsync_SavesConflictQueue()
    {
        var client = new FakeAdminApiClient
        {
            SavedAnalystWorkflow = new AnalystWorkflowDto
            {
                Id = "workflow_quality_1",
                Name = "Monitor backlog",
                WorkflowType = AnalystWorkspacePresentation.WorkflowTypeConflictReviewQueue,
                RoutePath = "/Quality/Index",
                PrimaryCategoryKey = "monitor",
                SelectedCategoryKeys = ["monitor"],
                State = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["category"] = "monitor"
                }
            }
        };

        var model = new ProductNormaliser.Web.Pages.Quality.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.Quality.IndexModel>.Instance)
        {
            CategoryKey = "monitor",
            SaveViewName = "Monitor backlog"
        };

        var result = await model.OnPostSaveViewAsync(AnalystWorkspacePresentation.WorkflowTypeConflictReviewQueue, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(client.LastSavedAnalystWorkflowRequest, Is.Not.Null);
            Assert.That(client.LastSavedAnalystWorkflowRequest!.WorkflowType, Is.EqualTo(AnalystWorkspacePresentation.WorkflowTypeConflictReviewQueue));
            Assert.That(client.LastSavedAnalystWorkflowRequest.State["category"], Is.EqualTo("monitor"));
            Assert.That(result, Is.TypeOf<RedirectToPageResult>());
        });
    }
}
