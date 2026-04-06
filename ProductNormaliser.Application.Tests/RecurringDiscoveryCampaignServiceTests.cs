using Microsoft.Extensions.Options;
using ProductNormaliser.Application.Categories;
using ProductNormaliser.Application.Governance;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.Discovery)]
public sealed class RecurringDiscoveryCampaignServiceTests
{
    [Test]
    public async Task CreateAsync_NormalizesCampaignIdentityAndSchedulesImmediately()
    {
        var store = new FakeDiscoveryCampaignStore();
        var discoveryRunService = new RecordingDiscoveryRunService();
        var service = CreateService(store, discoveryRunService);

        var campaign = await service.CreateAsync(new CreateRecurringDiscoveryCampaignRequest
        {
            CategoryKeys = [" tv ", "tv"],
            Market = " UK ",
            Locale = " en-GB ",
            BrandHints = [" Sony ", "sony"],
            AutomationMode = SourceAutomationModes.SuggestAccept,
            IntervalMinutes = 30
        });

        Assert.Multiple(() =>
        {
            Assert.That(campaign.CategoryKeys, Is.EqualTo(new[] { "tv" }));
            Assert.That(campaign.BrandHints, Is.EqualTo(new[] { "Sony" }));
            Assert.That(campaign.CampaignFingerprint, Is.EqualTo("market:uk|locale:en-gb|categories:tv|brands:sony"));
            Assert.That(campaign.NextScheduledUtc, Is.Not.Null);
            Assert.That(campaign.ResolveIntervalMinutes(), Is.EqualTo(30));
            Assert.That(campaign.LastRunId, Does.StartWith("discovery_run_"));
            Assert.That(campaign.LastScheduledUtc, Is.Not.Null);
            Assert.That(campaign.StatusMessage, Does.Contain($"queued initial run '{campaign.LastRunId}'"));
            Assert.That(discoveryRunService.ScheduledCampaignIds, Is.EqualTo(new[] { campaign.CampaignId }));
        });
    }

    [Test]
    public void CreateAsync_RejectsDuplicateCampaignFingerprint()
    {
        var store = new FakeDiscoveryCampaignStore(new RecurringDiscoveryCampaign
        {
            CampaignId = "campaign_1",
            Name = "TV UK",
            CategoryKeys = ["tv"],
            Market = "UK",
            Locale = "en-GB",
            BrandHints = ["Sony"],
            CampaignFingerprint = "market:uk|locale:en-gb|categories:tv|brands:sony"
        });
        var service = CreateService(store);

        var action = async () => await service.CreateAsync(new CreateRecurringDiscoveryCampaignRequest
        {
            CategoryKeys = ["tv"],
            Market = "UK",
            Locale = "en-GB",
            BrandHints = ["Sony"]
        });

        Assert.That(action, Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public async Task UpdateConfigurationAsync_RecalculatesNextWindowAndUpdatesCandidateCap()
    {
        var existingCampaign = new RecurringDiscoveryCampaign
        {
            CampaignId = "campaign_1",
            Name = "TV UK",
            CategoryKeys = ["tv"],
            Market = "UK",
            Locale = "en-GB",
            AutomationMode = SourceAutomationModes.SuggestAccept,
            MaxCandidatesPerRun = 12,
            IntervalMinutes = 24 * 60,
            IntervalHours = 24,
            Status = RecurringDiscoveryCampaignStatuses.Active,
            CampaignFingerprint = "market:uk|locale:en-gb|categories:tv|brands:sony",
            NextScheduledUtc = DateTime.UtcNow.AddHours(24),
            CreatedUtc = DateTime.UtcNow.AddDays(-2),
            UpdatedUtc = DateTime.UtcNow.AddHours(-1)
        };

        var service = CreateService(new FakeDiscoveryCampaignStore(existingCampaign));
        var beforeUpdateUtc = DateTime.UtcNow;

        var campaign = await service.UpdateConfigurationAsync("campaign_1", 30, 18, CancellationToken.None);

        Assert.That(campaign, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(campaign!.ResolveIntervalMinutes(), Is.EqualTo(30));
            Assert.That(campaign.IntervalHours, Is.EqualTo(0));
            Assert.That(campaign.MaxCandidatesPerRun, Is.EqualTo(18));
            Assert.That(campaign.NextScheduledUtc, Is.Not.Null);
            Assert.That(campaign.NextScheduledUtc, Is.GreaterThanOrEqualTo(beforeUpdateUtc.AddMinutes(29)));
            Assert.That(campaign.NextScheduledUtc, Is.LessThanOrEqualTo(DateTime.UtcNow.AddMinutes(31)));
            Assert.That(campaign.StatusMessage, Does.Contain("every 30 minutes"));
            Assert.That(campaign.StatusMessage, Does.Contain("cap of 18 candidates per run"));
        });
    }

    [Test]
    public async Task UpdateConfigurationAsync_UpdatesCandidateCapWithoutReschedulingWhenCadenceIsUnchanged()
    {
        var existingNextScheduledUtc = DateTime.UtcNow.AddMinutes(45);
        var existingCampaign = new RecurringDiscoveryCampaign
        {
            CampaignId = "campaign_1",
            Name = "TV UK",
            CategoryKeys = ["tv"],
            Market = "UK",
            Locale = "en-GB",
            AutomationMode = SourceAutomationModes.SuggestAccept,
            MaxCandidatesPerRun = 12,
            IntervalMinutes = 30,
            IntervalHours = 0,
            Status = RecurringDiscoveryCampaignStatuses.Active,
            CampaignFingerprint = "market:uk|locale:en-gb|categories:tv|brands:sony",
            NextScheduledUtc = existingNextScheduledUtc,
            CreatedUtc = DateTime.UtcNow.AddDays(-2),
            UpdatedUtc = DateTime.UtcNow.AddHours(-1)
        };

        var service = CreateService(new FakeDiscoveryCampaignStore(existingCampaign));

        var campaign = await service.UpdateConfigurationAsync("campaign_1", null, 20, CancellationToken.None);

        Assert.That(campaign, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(campaign!.ResolveIntervalMinutes(), Is.EqualTo(30));
            Assert.That(campaign.MaxCandidatesPerRun, Is.EqualTo(20));
            Assert.That(campaign.NextScheduledUtc, Is.EqualTo(existingNextScheduledUtc));
        });
    }

    [Test]
    public async Task DeleteAsync_RemovesCampaignAndPreservesRunHistorySemantics()
    {
        var store = new FakeDiscoveryCampaignStore(new RecurringDiscoveryCampaign
        {
            CampaignId = "campaign_1",
            Name = "TV UK",
            CategoryKeys = ["tv"],
            CampaignFingerprint = "market:uk|locale:en-gb|categories:tv|brands:sony"
        });
        var service = CreateService(store);

        var deleted = await service.DeleteAsync("campaign_1", CancellationToken.None);
        var remainingCampaign = await store.GetAsync("campaign_1", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.True);
            Assert.That(remainingCampaign, Is.Null);
        });
    }

    private static RecurringDiscoveryCampaignService CreateService(FakeDiscoveryCampaignStore store, RecordingDiscoveryRunService? discoveryRunService = null)
    {
        return new RecurringDiscoveryCampaignService(
            store,
            new FakeCategoryMetadataService(new CategoryMetadata { CategoryKey = "tv", DisplayName = "TV", IsEnabled = true }),
            discoveryRunService ?? new RecordingDiscoveryRunService(),
            new RecordingAuditService(),
            Options.Create(new DiscoveryRunOperationsOptions()));
    }

    private sealed class FakeDiscoveryCampaignStore(params RecurringDiscoveryCampaign[] campaigns) : IDiscoveryCampaignStore
    {
        private readonly Dictionary<string, RecurringDiscoveryCampaign> items = campaigns.ToDictionary(campaign => campaign.CampaignId, StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<RecurringDiscoveryCampaign>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RecurringDiscoveryCampaign>>(items.Values.ToArray());

        public Task<IReadOnlyList<RecurringDiscoveryCampaign>> ListByStatusesAsync(IReadOnlyCollection<string> statuses, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RecurringDiscoveryCampaign>>(items.Values.Where(campaign => statuses.Contains(campaign.Status)).ToArray());

        public Task<IReadOnlyList<RecurringDiscoveryCampaign>> ListDueAsync(DateTime utcNow, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RecurringDiscoveryCampaign>>(items.Values.Where(campaign => campaign.NextScheduledUtc <= utcNow).Take(limit).ToArray());

        public Task<RecurringDiscoveryCampaign?> GetAsync(string campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult(items.TryGetValue(campaignId, out var campaign) ? campaign : null);

        public Task<RecurringDiscoveryCampaign?> GetByFingerprintAsync(string campaignFingerprint, CancellationToken cancellationToken = default)
            => Task.FromResult(items.Values.FirstOrDefault(campaign => string.Equals(campaign.CampaignFingerprint, campaignFingerprint, StringComparison.OrdinalIgnoreCase)));

        public Task UpsertAsync(RecurringDiscoveryCampaign campaign, CancellationToken cancellationToken = default)
        {
            items[campaign.CampaignId] = campaign;
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(string campaignId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(items.Remove(campaignId));
        }
    }

    private sealed class FakeCategoryMetadataService(params CategoryMetadata[] categories) : ICategoryMetadataService
    {
        private readonly IReadOnlyList<CategoryMetadata> items = categories;

        public Task EnsureDefaultsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<CategoryMetadata>> ListAsync(bool enabledOnly = false, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CategoryMetadata>>(items);

        public Task<CategoryMetadata?> GetAsync(string categoryKey, CancellationToken cancellationToken = default)
            => Task.FromResult(items.FirstOrDefault(category => string.Equals(category.CategoryKey, categoryKey, StringComparison.OrdinalIgnoreCase)));

        public Task<CategoryMetadata> UpsertAsync(CategoryMetadata categoryMetadata, CancellationToken cancellationToken = default)
            => Task.FromResult(categoryMetadata);
    }

    private sealed class RecordingAuditService : IManagementAuditService
    {
        public Task RecordAsync(string action, string targetType, string targetId, IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ManagementAuditEntry>> ListRecentAsync(int take = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ManagementAuditEntry>>([]);
    }

    private sealed class RecordingDiscoveryRunService : IDiscoveryRunService
    {
        public List<string> ScheduledCampaignIds { get; } = [];

        public Task<DiscoveryRun> CreateAsync(ProductNormaliser.Application.Sources.CreateDiscoveryRunRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DiscoveryRun> CreateScheduledAsync(RecurringDiscoveryCampaign campaign, CancellationToken cancellationToken = default)
        {
            ScheduledCampaignIds.Add(campaign.CampaignId);
            return Task.FromResult(new DiscoveryRun
            {
                RunId = $"discovery_run_{campaign.CampaignId.Replace("discovery_campaign_", string.Empty, StringComparison.OrdinalIgnoreCase)}",
                TriggerKind = DiscoveryRunTriggerKinds.RecurringCampaign,
                RecurringCampaignId = campaign.CampaignId,
                RequestedCategoryKeys = campaign.CategoryKeys,
                Market = campaign.Market,
                Locale = campaign.Locale,
                AutomationMode = campaign.AutomationMode,
                BrandHints = campaign.BrandHints,
                MaxCandidates = campaign.MaxCandidatesPerRun,
                Status = DiscoveryRunStatuses.Queued,
                CurrentStage = DiscoveryRunStageNames.Search,
                StatusMessage = "Queued.",
                LlmStatus = "disabled",
                LlmStatusMessage = "Disabled.",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            });
        }

        public Task<DiscoveryRunPage> ListAsync(DiscoveryRunQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DiscoveryRun?> GetAsync(string runId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DiscoveryRunCandidate>> ListCandidatesAsync(string runId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DiscoveryRunCandidatePage> QueryCandidatesAsync(string runId, DiscoveryRunCandidateQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DiscoveryRun?> PauseAsync(string runId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DiscoveryRun?> ResumeAsync(string runId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DiscoveryRun?> StopAsync(string runId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DiscoveryRunCandidate?> AcceptCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DiscoveryRunCandidate?> DismissCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DiscoveryRunCandidate?> RestoreCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}