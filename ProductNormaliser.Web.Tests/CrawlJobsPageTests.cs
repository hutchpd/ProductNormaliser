using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Models;

namespace ProductNormaliser.Web.Tests;

public sealed class CrawlJobsPageTests
{
    [Test]
    public async Task CrawlJobsIndex_OnPostLaunchAsync_CreatesJobAndRedirectsToDetails()
    {
        var client = CreateClient();
        client.CreatedJob = new CrawlJobDto { JobId = "job_123", Status = "pending" };

        var model = new ProductNormaliser.Web.Pages.CrawlJobs.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.CrawlJobs.IndexModel>.Instance)
        {
            Launch = new ProductNormaliser.Web.Pages.CrawlJobs.IndexModel.LaunchCrawlJobInput
            {
                SelectedCategoryKeys = ["tv"],
                SelectedSourceIds = ["ao_uk"]
            }
        };

        var result = await model.OnPostLaunchAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(client.LastCreatedJobRequest, Is.Not.Null);
            Assert.That(client.LastCreatedJobRequest!.RequestedCategories, Is.EqualTo(new[] { "tv" }));
            Assert.That(client.LastCreatedJobRequest.RequestedSources, Is.EqualTo(new[] { "ao_uk" }));
            Assert.That(result, Is.TypeOf<RedirectToPageResult>());
            Assert.That(((RedirectToPageResult)result).PageName, Is.EqualTo("/CrawlJobs/Details"));
        });
    }

    [Test]
    public async Task CrawlJobsIndex_OnPostLaunchAsync_ReturnsPageForEmptySelection()
    {
        var client = CreateClient();
        var model = new ProductNormaliser.Web.Pages.CrawlJobs.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.CrawlJobs.IndexModel>.Instance)
        {
            Launch = new ProductNormaliser.Web.Pages.CrawlJobs.IndexModel.LaunchCrawlJobInput()
        };

        var result = await model.OnPostLaunchAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<PageResult>());
            Assert.That(model.ModelState[$"{nameof(model.Launch)}.{nameof(model.Launch.SelectedCategoryKeys)}"]?.Errors.Select(error => error.ErrorMessage), Does.Contain("Choose at least one category before launching a crawl."));
        });
    }

    [Test]
    public async Task CrawlJobsIndex_OnPostLaunchAsync_ShowsApiFailureState()
    {
        var client = CreateClient();
        client.CreateJobException = new ProductNormaliser.Web.Services.AdminApiException("Admin API is unavailable.");

        var model = new ProductNormaliser.Web.Pages.CrawlJobs.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.CrawlJobs.IndexModel>.Instance)
        {
            Launch = new ProductNormaliser.Web.Pages.CrawlJobs.IndexModel.LaunchCrawlJobInput
            {
                SelectedCategoryKeys = ["tv"]
            }
        };

        var result = await model.OnPostLaunchAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<PageResult>());
            Assert.That(model.ErrorMessage, Is.EqualTo("Admin API is unavailable."));
        });
    }

    [Test]
    public async Task CrawlJobsIndex_OnPostLaunchAsync_RejectsIncompatiblePostedSourceSelection()
    {
        var client = CreateClient();
        client.Sources =
        [
            new SourceDto
            {
                SourceId = "ao_uk",
                DisplayName = "AO UK",
                IsEnabled = true,
                SupportedCategoryKeys = ["tv"]
            },
            new SourceDto
            {
                SourceId = "fridge_world",
                DisplayName = "Fridge World",
                IsEnabled = true,
                SupportedCategoryKeys = ["refrigerator"]
            }
        ];

        var model = new ProductNormaliser.Web.Pages.CrawlJobs.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.CrawlJobs.IndexModel>.Instance)
        {
            Launch = new ProductNormaliser.Web.Pages.CrawlJobs.IndexModel.LaunchCrawlJobInput
            {
                SelectedCategoryKeys = ["tv"],
                SelectedSourceIds = ["fridge_world"]
            }
        };

        var result = await model.OnPostLaunchAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<PageResult>());
            Assert.That(client.LastCreatedJobRequest, Is.Null);
            Assert.That(model.ModelState[$"{nameof(model.Launch)}.{nameof(model.Launch.SelectedSourceIds)}"]?.Errors.Select(error => error.ErrorMessage), Does.Contain("Selected sources do not support the chosen categories: Fridge World."));
        });
    }

    [Test]
    public async Task CrawlJobsIndex_OnGetAsync_GroupsProgressStatesAndEnablesPollingForActiveJobs()
    {
        var client = CreateClient();
        client.CrawlJobsPage = new CrawlJobListResponseDto
        {
            Items =
            [
                CreateJob("job_active", "running", 10, 4),
                CreateJob("job_done", "completed", 8, 8),
                CreateJob("job_fail", "failed", 9, 9, failedCount: 3)
            ],
            Page = 1,
            PageSize = 30,
            TotalCount = 3,
            TotalPages = 1
        };

        var model = new ProductNormaliser.Web.Pages.CrawlJobs.IndexModel(client, NullLogger<ProductNormaliser.Web.Pages.CrawlJobs.IndexModel>.Instance);

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(model.ActiveJobs.Select(job => job.JobId), Is.EqualTo(new[] { "job_active" }));
            Assert.That(model.CompletedJobs.Select(job => job.JobId), Is.EqualTo(new[] { "job_done" }));
            Assert.That(model.FailedJobs.Select(job => job.JobId), Is.EqualTo(new[] { "job_fail" }));
            Assert.That(model.ShouldAutoRefresh, Is.True);
        });
    }

    [Test]
    public async Task CrawlJobDetails_OnGetAsync_ShowsCompletedJobProgress()
    {
        var client = CreateClient();
        client.CrawlJob = CreateJob("job_done", "completed", 12, 12, successCount: 10, skippedCount: 2);

        var model = new ProductNormaliser.Web.Pages.CrawlJobs.DetailsModel(client, NullLogger<ProductNormaliser.Web.Pages.CrawlJobs.DetailsModel>.Instance)
        {
            JobId = "job_done"
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(model.Job, Is.Not.Null);
            Assert.That(model.ProgressPercent, Is.EqualTo(100));
            Assert.That(model.StatusBadge.Text, Is.EqualTo("Completed"));
            Assert.That(model.ShouldAutoRefresh, Is.False);
        });
    }

    [Test]
    public async Task CrawlJobDetails_OnGetAsync_ShowsFailedJobRenderingState()
    {
        var client = CreateClient();
        client.CrawlJob = CreateJob("job_fail", "failed", 10, 10, successCount: 5, failedCount: 5);

        var model = new ProductNormaliser.Web.Pages.CrawlJobs.DetailsModel(client, NullLogger<ProductNormaliser.Web.Pages.CrawlJobs.DetailsModel>.Instance)
        {
            JobId = "job_fail"
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(model.Job, Is.Not.Null);
            Assert.That(model.StatusBadge.Text, Is.EqualTo("Failed"));
            Assert.That(model.StatusBadge.Tone, Is.EqualTo("danger"));
            Assert.That(model.ShouldAutoRefresh, Is.False);
        });
    }

    [Test]
    public void CrawlJobPresentation_ReturnsExpectedProgressState()
    {
        var badge = CrawlJobPresentation.GetStatusBadge("cancel_requested");
        var percent = CrawlJobPresentation.GetProgressPercent(CreateJob("job_active", "running", 20, 5));

        Assert.Multiple(() =>
        {
            Assert.That(badge.Text, Is.EqualTo("Cancel requested"));
            Assert.That(badge.Tone, Is.EqualTo("warning"));
            Assert.That(percent, Is.EqualTo(25));
        });
    }

    private static FakeAdminApiClient CreateClient()
    {
        return new FakeAdminApiClient
        {
            Categories =
            [
                new CategoryMetadataDto
                {
                    CategoryKey = "tv",
                    DisplayName = "TVs",
                    FamilyKey = "display",
                    FamilyDisplayName = "Display",
                    IsEnabled = true,
                    CrawlSupportStatus = "Supported",
                    SchemaCompletenessScore = 0.95m
                }
            ],
            Sources =
            [
                new SourceDto
                {
                    SourceId = "ao_uk",
                    DisplayName = "AO UK",
                    IsEnabled = true,
                    SupportedCategoryKeys = ["tv"]
                }
            ]
        };
    }

    private static CrawlJobDto CreateJob(string jobId, string status, int totalTargets, int processedTargets, int successCount = 0, int skippedCount = 0, int failedCount = 0, int cancelledCount = 0)
    {
        return new CrawlJobDto
        {
            JobId = jobId,
            RequestType = "category",
            RequestedCategories = ["tv"],
            RequestedSources = ["ao_uk"],
            TotalTargets = totalTargets,
            ProcessedTargets = processedTargets,
            SuccessCount = successCount,
            SkippedCount = skippedCount,
            FailedCount = failedCount,
            CancelledCount = cancelledCount,
            StartedAt = new DateTime(2026, 03, 21, 10, 00, 00, DateTimeKind.Utc),
            LastUpdatedAt = new DateTime(2026, 03, 21, 10, 05, 00, DateTimeKind.Utc),
            Status = status,
            PerCategoryBreakdown =
            [
                new CrawlJobCategoryBreakdownDto
                {
                    CategoryKey = "tv",
                    TotalTargets = totalTargets,
                    ProcessedTargets = processedTargets,
                    SuccessCount = successCount,
                    SkippedCount = skippedCount,
                    FailedCount = failedCount,
                    CancelledCount = cancelledCount
                }
            ]
        };
    }
}