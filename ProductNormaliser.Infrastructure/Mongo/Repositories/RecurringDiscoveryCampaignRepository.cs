using MongoDB.Driver;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class RecurringDiscoveryCampaignRepository(MongoDbContext context)
    : MongoRepositoryBase<RecurringDiscoveryCampaign>(context.RecurringDiscoveryCampaigns), IDiscoveryCampaignStore
{
    public async Task<IReadOnlyList<RecurringDiscoveryCampaign>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await Collection.Find(Builders<RecurringDiscoveryCampaign>.Filter.Empty)
            .SortBy(campaign => campaign.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecurringDiscoveryCampaign>> ListByStatusesAsync(IReadOnlyCollection<string> statuses, CancellationToken cancellationToken = default)
    {
        if (statuses.Count == 0)
        {
            return [];
        }

        return await Collection.Find(campaign => statuses.Contains(campaign.Status))
            .SortBy(campaign => campaign.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecurringDiscoveryCampaign>> ListDueAsync(DateTime utcNow, int limit, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(campaign => campaign.Status == RecurringDiscoveryCampaignStatuses.Active
                && campaign.NextScheduledUtc != null
                && campaign.NextScheduledUtc <= utcNow)
            .SortBy(campaign => campaign.NextScheduledUtc)
            .Limit(Math.Max(1, limit))
            .ToListAsync(cancellationToken);
    }

    public async Task<RecurringDiscoveryCampaign?> GetAsync(string campaignId, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(campaign => campaign.CampaignId == campaignId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<RecurringDiscoveryCampaign?> GetByFingerprintAsync(string campaignFingerprint, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(campaign => campaign.CampaignFingerprint == campaignFingerprint).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertAsync(RecurringDiscoveryCampaign campaign, CancellationToken cancellationToken = default)
    {
        await Collection.ReplaceOneAsync(existing => existing.CampaignId == campaign.CampaignId, campaign, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }
}