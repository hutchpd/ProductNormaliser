using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

public interface IDiscoveryCampaignStore
{
    Task<IReadOnlyList<RecurringDiscoveryCampaign>> ListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecurringDiscoveryCampaign>> ListByStatusesAsync(IReadOnlyCollection<string> statuses, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecurringDiscoveryCampaign>> ListDueAsync(DateTime utcNow, int limit, CancellationToken cancellationToken = default);

    Task<RecurringDiscoveryCampaign?> GetAsync(string campaignId, CancellationToken cancellationToken = default);

    Task<RecurringDiscoveryCampaign?> GetByFingerprintAsync(string campaignFingerprint, CancellationToken cancellationToken = default);

    Task UpsertAsync(RecurringDiscoveryCampaign campaign, CancellationToken cancellationToken = default);
}