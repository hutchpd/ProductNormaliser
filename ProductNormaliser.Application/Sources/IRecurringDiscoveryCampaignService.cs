using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

public interface IRecurringDiscoveryCampaignService
{
    Task<IReadOnlyList<RecurringDiscoveryCampaign>> ListAsync(string? status = null, CancellationToken cancellationToken = default);

    Task<RecurringDiscoveryCampaign?> GetAsync(string campaignId, CancellationToken cancellationToken = default);

    Task<RecurringDiscoveryCampaign> CreateAsync(CreateRecurringDiscoveryCampaignRequest request, CancellationToken cancellationToken = default);

    Task<RecurringDiscoveryCampaign?> UpdateConfigurationAsync(string campaignId, int? intervalMinutes, int? maxCandidatesPerRun, CancellationToken cancellationToken = default);

    Task<RecurringDiscoveryCampaign?> PauseAsync(string campaignId, CancellationToken cancellationToken = default);

    Task<RecurringDiscoveryCampaign?> ResumeAsync(string campaignId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string campaignId, CancellationToken cancellationToken = default);
}