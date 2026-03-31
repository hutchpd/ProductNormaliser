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
        var service = CreateService(store);

        var campaign = await service.CreateAsync(new CreateRecurringDiscoveryCampaignRequest
        {
            CategoryKeys = [" tv ", "tv"],
            Market = " UK ",
            Locale = " en-GB ",
            BrandHints = [" Sony ", "sony"],
            AutomationMode = SourceAutomationModes.SuggestAccept,
            IntervalHours = 12
        });

        Assert.Multiple(() =>
        {
            Assert.That(campaign.CategoryKeys, Is.EqualTo(new[] { "tv" }));
            Assert.That(campaign.BrandHints, Is.EqualTo(new[] { "Sony" }));
            Assert.That(campaign.CampaignFingerprint, Is.EqualTo("market:uk|locale:en-gb|categories:tv|brands:sony"));
            Assert.That(campaign.NextScheduledUtc, Is.Not.Null);
            Assert.That(campaign.IntervalHours, Is.EqualTo(12));
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

    private static RecurringDiscoveryCampaignService CreateService(FakeDiscoveryCampaignStore store)
    {
        return new RecurringDiscoveryCampaignService(
            store,
            new FakeCategoryMetadataService(new CategoryMetadata { CategoryKey = "tv", DisplayName = "TV", IsEnabled = true }),
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
}