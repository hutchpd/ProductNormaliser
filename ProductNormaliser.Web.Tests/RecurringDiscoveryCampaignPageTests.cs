using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Tests;

public sealed class RecurringDiscoveryCampaignPageTests
{
    [Test]
    public async Task OnGetAsync_LoadsCampaignInventory()
    {
        var client = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            RecurringDiscoveryCampaigns = [CreateCampaign("campaign_1", "active"), CreateCampaign("campaign_2", "paused")]
        };

        var model = new ProductNormaliser.Web.Pages.Sources.RecurringDiscoveryCampaigns.IndexModel(
            client,
            NullLogger<ProductNormaliser.Web.Pages.Sources.RecurringDiscoveryCampaigns.IndexModel>.Instance);

        await model.OnGetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(model.Campaigns, Has.Count.EqualTo(2));
            Assert.That(model.ActiveCampaignCount, Is.EqualTo(1));
            Assert.That(model.PausedCampaignCount, Is.EqualTo(1));
            Assert.That(model.Categories, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task OnPostCreateAsync_CreatesCampaignAndRedirects()
    {
        var client = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            RecurringDiscoveryCampaign = CreateCampaign("campaign_created", "active")
        };

        var model = new ProductNormaliser.Web.Pages.Sources.RecurringDiscoveryCampaigns.IndexModel(
            client,
            NullLogger<ProductNormaliser.Web.Pages.Sources.RecurringDiscoveryCampaigns.IndexModel>.Instance)
        {
            Campaign = new ProductNormaliser.Web.Pages.Sources.RecurringDiscoveryCampaigns.IndexModel.CreateCampaignInput
            {
                Name = "TV UK",
                CategoryKeys = ["tv"],
                Market = "UK",
                Locale = "en-GB",
                BrandHints = "Sony",
                AutomationMode = "suggest_accept",
                MaxCandidatesPerRun = 12,
                IntervalHours = 24
            }
        };

        var result = await model.OnPostCreateAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<RedirectToPageResult>());
            Assert.That(client.LastCreateRecurringDiscoveryCampaignRequest, Is.Not.Null);
            Assert.That(client.LastCreateRecurringDiscoveryCampaignRequest!.CategoryKeys, Is.EqualTo(new[] { "tv" }));
            Assert.That(client.LastCreateRecurringDiscoveryCampaignRequest.BrandHints, Is.EqualTo(new[] { "Sony" }));
            Assert.That(model.StatusMessage, Does.Contain("Created recurring discovery campaign"));
            Assert.That(model.StatusMessage, Does.Contain("Initial run 'discovery_run_1' is queued now"));
        });
    }

    [Test]
    public void GetMemorySummary_ShowsQueuedInitialRun_WhenNoHistoryExistsYet()
    {
        var model = new ProductNormaliser.Web.Pages.Sources.RecurringDiscoveryCampaigns.IndexModel(
            new FakeAdminApiClient(),
            NullLogger<ProductNormaliser.Web.Pages.Sources.RecurringDiscoveryCampaigns.IndexModel>.Instance);

        var summary = model.GetMemorySummary(new RecurringDiscoveryCampaignDto
        {
            CampaignId = "campaign_1",
            Name = "TV UK",
            CategoryKeys = ["tv"],
            AutomationMode = "suggest_accept",
            Status = "active",
            CampaignFingerprint = "market:uk|locale:en-gb|categories:tv|brands:",
            LastRunId = "discovery_run_1",
            HistoricalRunCount = 0,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        });

        Assert.That(summary, Is.EqualTo("No historical runs yet. Initial run 'discovery_run_1' is queued or in progress."));
    }

    [Test]
    public async Task OnPostDeleteAsync_DeletesCampaignAndRedirects()
    {
        var client = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            RecurringDiscoveryCampaigns = [CreateCampaign("campaign_1", "active")]
        };

        var model = new ProductNormaliser.Web.Pages.Sources.RecurringDiscoveryCampaigns.IndexModel(
            client,
            NullLogger<ProductNormaliser.Web.Pages.Sources.RecurringDiscoveryCampaigns.IndexModel>.Instance);

        var result = await model.OnPostDeleteAsync("campaign_1", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<RedirectToPageResult>());
            Assert.That(client.LastDeletedRecurringDiscoveryCampaignId, Is.EqualTo("campaign_1"));
            Assert.That(model.StatusMessage, Does.Contain("Historical discovery runs were preserved"));
        });
    }

    [Test]
    public async Task OnPostPauseAsync_PausesCampaignAndRedirects()
    {
        var client = new FakeAdminApiClient
        {
            Categories = CreateCategories(),
            RecurringDiscoveryCampaigns = [CreateCampaign("campaign_1", "active")]
        };

        var model = new ProductNormaliser.Web.Pages.Sources.RecurringDiscoveryCampaigns.IndexModel(
            client,
            NullLogger<ProductNormaliser.Web.Pages.Sources.RecurringDiscoveryCampaigns.IndexModel>.Instance);

        var result = await model.OnPostPauseAsync("campaign_1", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<RedirectToPageResult>());
            Assert.That(client.LastPausedRecurringDiscoveryCampaignId, Is.EqualTo("campaign_1"));
            Assert.That(model.StatusMessage, Does.Contain("Paused recurring discovery campaign"));
        });
    }

    private static IReadOnlyList<CategoryMetadataDto> CreateCategories()
    {
        return
        [
            new CategoryMetadataDto
            {
                CategoryKey = "tv",
                DisplayName = "TVs",
                FamilyKey = "display",
                FamilyDisplayName = "Display",
                IconKey = "tv",
                CrawlSupportStatus = "Supported",
                SchemaCompletenessScore = 0.95m,
                IsEnabled = true
            },
            new CategoryMetadataDto
            {
                CategoryKey = "monitor",
                DisplayName = "Monitors",
                FamilyKey = "display",
                FamilyDisplayName = "Display",
                IconKey = "monitor",
                CrawlSupportStatus = "Supported",
                SchemaCompletenessScore = 0.93m,
                IsEnabled = true
            }
        ];
    }

    private static RecurringDiscoveryCampaignDto CreateCampaign(string campaignId, string status)
    {
        return new RecurringDiscoveryCampaignDto
        {
            CampaignId = campaignId,
            Name = "TV UK",
            CategoryKeys = ["tv"],
            Market = "UK",
            Locale = "en-GB",
            BrandHints = ["Sony"],
            AutomationMode = "suggest_accept",
            MaxCandidatesPerRun = 12,
            IntervalHours = 24,
            Status = status,
            CampaignFingerprint = "market:uk|locale:en-gb|categories:tv|brands:sony",
            LastRunId = "discovery_run_1",
            StatusMessage = "Campaign status.",
            HistoricalRunCount = 3,
            CompletedRunCount = 3,
            AcceptedCandidateCount = 2,
            DismissedCandidateCount = 1,
            SupersededCandidateCount = 0,
            ArchivedCandidateCount = 0,
            RunsWithAcceptedCandidates = 2,
            RunsWithoutAcceptedCandidates = 1,
            LastCompletedUtc = DateTime.UtcNow.AddHours(-1),
            LastAcceptedUtc = DateTime.UtcNow.AddHours(-2),
            CreatedUtc = DateTime.UtcNow.AddDays(-5),
            UpdatedUtc = DateTime.UtcNow.AddMinutes(-20),
            LastScheduledUtc = DateTime.UtcNow.AddHours(-12),
            NextScheduledUtc = DateTime.UtcNow.AddHours(12)
        };
    }
}