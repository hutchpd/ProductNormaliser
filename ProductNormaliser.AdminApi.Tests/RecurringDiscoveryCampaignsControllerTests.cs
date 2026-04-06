using Microsoft.AspNetCore.Mvc;
using ProductNormaliser.AdminApi.Contracts;
using ProductNormaliser.AdminApi.Controllers;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.AdminApi.Tests;

public sealed class RecurringDiscoveryCampaignsControllerTests
{
    [Test]
    public async Task Create_ReturnsCreatedCampaignContract()
    {
        var service = new FakeRecurringDiscoveryCampaignService
        {
            Campaign = CreateCampaign()
        };
        var controller = new RecurringDiscoveryCampaignsController(service);

        var result = await controller.Create(new ProductNormaliser.AdminApi.Contracts.CreateRecurringDiscoveryCampaignRequest
        {
            CategoryKeys = ["tv"],
            Market = "UK",
            Locale = "en-GB",
            BrandHints = ["Sony"],
            AutomationMode = SourceAutomationModes.SuggestAccept,
            IntervalMinutes = 30,
            MaxCandidatesPerRun = 12
        }, CancellationToken.None);

        Assert.That(result, Is.TypeOf<CreatedAtActionResult>());
        var created = (CreatedAtActionResult)result;
        var dto = created.Value as RecurringDiscoveryCampaignDto;

        Assert.Multiple(() =>
        {
            Assert.That(created.ActionName, Is.EqualTo("Get"));
            Assert.That(dto, Is.Not.Null);
            Assert.That(dto!.CampaignId, Is.EqualTo("campaign_1"));
            Assert.That(dto.BrandHints, Is.EqualTo(new[] { "Sony" }));
            Assert.That(dto.AcceptedCandidateCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task Pause_ReturnsConflictWhenServiceRejectsTransition()
    {
        var controller = new RecurringDiscoveryCampaignsController(new FakeRecurringDiscoveryCampaignService
        {
            PauseException = new InvalidOperationException("Already paused.")
        });

        var result = await controller.Pause("campaign_1", CancellationToken.None);

        Assert.That(result, Is.TypeOf<ConflictObjectResult>());
    }

    [Test]
    public async Task UpdateSchedule_ReturnsUpdatedCampaignContract()
    {
        var service = new FakeRecurringDiscoveryCampaignService
        {
            Campaign = CreateCampaign()
        };
        var controller = new RecurringDiscoveryCampaignsController(service);

        var result = await controller.UpdateSchedule("campaign_1", new UpdateRecurringDiscoveryCampaignScheduleRequest
        {
            IntervalMinutes = 60
        }, CancellationToken.None);

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var ok = (OkObjectResult)result;
        var dto = ok.Value as RecurringDiscoveryCampaignDto;

        Assert.Multiple(() =>
        {
            Assert.That(service.LastUpdatedScheduleCampaignId, Is.EqualTo("campaign_1"));
            Assert.That(service.LastUpdatedScheduleIntervalMinutes, Is.EqualTo(60));
            Assert.That(dto, Is.Not.Null);
            Assert.That(dto!.IntervalMinutes, Is.EqualTo(60));
        });
    }

    [Test]
    public async Task Delete_ReturnsNoContentWhenCampaignExists()
    {
        var service = new FakeRecurringDiscoveryCampaignService
        {
            Campaign = CreateCampaign()
        };
        var controller = new RecurringDiscoveryCampaignsController(service);

        var result = await controller.Delete("campaign_1", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<NoContentResult>());
            Assert.That(service.LastDeletedCampaignId, Is.EqualTo("campaign_1"));
        });
    }

    private static RecurringDiscoveryCampaign CreateCampaign()
    {
        return new RecurringDiscoveryCampaign
        {
            CampaignId = "campaign_1",
            Name = "TV UK",
            CategoryKeys = ["tv"],
            Market = "UK",
            Locale = "en-GB",
            BrandHints = ["Sony"],
            AutomationMode = SourceAutomationModes.SuggestAccept,
            MaxCandidatesPerRun = 12,
            IntervalMinutes = 30,
            Status = RecurringDiscoveryCampaignStatuses.Active,
            CampaignFingerprint = "market:uk|locale:en-gb|categories:tv|brands:sony",
            LastRunId = "discovery_run_42",
            StatusMessage = "Recurring discovery campaign has produced 2 accepted candidates across 3 runs.",
            Memory = new RecurringDiscoveryCampaignMemory
            {
                HistoricalRunCount = 3,
                CompletedRunCount = 3,
                AcceptedCandidateCount = 2,
                DismissedCandidateCount = 1,
                SupersededCandidateCount = 1,
                RunsWithAcceptedCandidates = 2,
                RunsWithoutAcceptedCandidates = 1,
                LastCompletedUtc = DateTime.UtcNow.AddHours(-2),
                LastAcceptedUtc = DateTime.UtcNow.AddHours(-3)
            },
            CreatedUtc = DateTime.UtcNow.AddDays(-5),
            UpdatedUtc = DateTime.UtcNow.AddHours(-1),
            LastScheduledUtc = DateTime.UtcNow.AddHours(-6),
            NextScheduledUtc = DateTime.UtcNow.AddMinutes(30)
        };
    }

    private sealed class FakeRecurringDiscoveryCampaignService : IRecurringDiscoveryCampaignService
    {
        public RecurringDiscoveryCampaign? Campaign { get; set; }
        public InvalidOperationException? PauseException { get; set; }
        public string? LastDeletedCampaignId { get; private set; }
        public string? LastUpdatedScheduleCampaignId { get; private set; }
        public int? LastUpdatedScheduleIntervalMinutes { get; private set; }

        public Task<IReadOnlyList<RecurringDiscoveryCampaign>> ListAsync(string? status = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RecurringDiscoveryCampaign>>(Campaign is null ? [] : [Campaign]);

        public Task<RecurringDiscoveryCampaign?> GetAsync(string campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult(Campaign is not null && string.Equals(Campaign.CampaignId, campaignId, StringComparison.OrdinalIgnoreCase) ? Campaign : null);

        public Task<RecurringDiscoveryCampaign> CreateAsync(ProductNormaliser.Application.Sources.CreateRecurringDiscoveryCampaignRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(Campaign ?? throw new InvalidOperationException("No campaign configured."));

        public Task<RecurringDiscoveryCampaign?> UpdateScheduleAsync(string campaignId, int intervalMinutes, CancellationToken cancellationToken = default)
        {
            LastUpdatedScheduleCampaignId = campaignId;
            LastUpdatedScheduleIntervalMinutes = intervalMinutes;
            if (Campaign is null || !string.Equals(Campaign.CampaignId, campaignId, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<RecurringDiscoveryCampaign?>(null);
            }

            Campaign.IntervalMinutes = intervalMinutes;
            Campaign.UpdatedUtc = DateTime.UtcNow;
            Campaign.NextScheduledUtc = DateTime.UtcNow.AddMinutes(intervalMinutes);
            return Task.FromResult<RecurringDiscoveryCampaign?>(Campaign);
        }

        public Task<RecurringDiscoveryCampaign?> PauseAsync(string campaignId, CancellationToken cancellationToken = default)
            => PauseException is null ? Task.FromResult(Campaign) : Task.FromException<RecurringDiscoveryCampaign?>(PauseException);

        public Task<RecurringDiscoveryCampaign?> ResumeAsync(string campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult(Campaign);

        public Task<bool> DeleteAsync(string campaignId, CancellationToken cancellationToken = default)
        {
            LastDeletedCampaignId = campaignId;
            var deleted = Campaign is not null && string.Equals(Campaign.CampaignId, campaignId, StringComparison.OrdinalIgnoreCase);
            if (deleted)
            {
                Campaign = null;
            }

            return Task.FromResult(deleted);
        }
    }
}