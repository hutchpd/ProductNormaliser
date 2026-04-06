using ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Tests;

public sealed class RecurringDiscoveryCampaignRenderingTests
{
    [Test]
    public async Task RecurringDiscoveryCampaignsIndex_RendersInventoryAndActions()
    {
        var fakeAdminApiClient = new FakeAdminApiClient
        {
            Categories =
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
                }
            ],
            RecurringDiscoveryCampaigns =
            [
                new RecurringDiscoveryCampaignDto
                {
                    CampaignId = "campaign_1",
                    Name = "TV UK",
                    CategoryKeys = ["tv"],
                    Market = "UK",
                    Locale = "en-GB",
                    BrandHints = ["Sony"],
                    AutomationMode = "suggest_accept",
                    MaxCandidatesPerRun = 12,
                    IntervalMinutes = 30,
                    Status = "active",
                    CampaignFingerprint = "market:uk|locale:en-gb|categories:tv|brands:sony",
                    LastRunId = "discovery_run_42",
                    StatusMessage = "Recurring discovery campaign has produced accepted candidates.",
                    HistoricalRunCount = 3,
                    CompletedRunCount = 3,
                    AcceptedCandidateCount = 2,
                    DismissedCandidateCount = 1,
                    SupersededCandidateCount = 0,
                    ArchivedCandidateCount = 0,
                    RunsWithAcceptedCandidates = 2,
                    RunsWithoutAcceptedCandidates = 1,
                    LastCompletedUtc = new DateTime(2026, 03, 31, 09, 00, 00, DateTimeKind.Utc),
                    LastAcceptedUtc = new DateTime(2026, 03, 31, 08, 00, 00, DateTimeKind.Utc),
                    CreatedUtc = new DateTime(2026, 03, 28, 10, 00, 00, DateTimeKind.Utc),
                    UpdatedUtc = new DateTime(2026, 03, 31, 09, 30, 00, DateTimeKind.Utc),
                    LastScheduledUtc = new DateTime(2026, 03, 31, 06, 00, 00, DateTimeKind.Utc),
                    NextScheduledUtc = new DateTime(2026, 04, 01, 06, 00, 00, DateTimeKind.Utc)
                }
            ]
        };

        await using var factory = new ProductWebApplicationFactory(fakeAdminApiClient);
        using var client = await factory.CreateOperatorClientAsync();

        var html = await client.GetStringAsync("/Sources/RecurringDiscoveryCampaigns/Index");

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("Manage recurring discovery campaigns"));
            Assert.That(html, Does.Contain("Create campaign"));
            Assert.That(html, Does.Contain("TV UK"));
            Assert.That(html, Does.Contain("Delete schedule"));
            Assert.That(html, Does.Contain("Open last run"));
            Assert.That(html, Does.Contain("Fresh runs, not infinite loops"));
            Assert.That(html, Does.Contain("Every 30 minutes"));
        });
    }
}