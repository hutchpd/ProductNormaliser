using MongoDB.Driver;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class AdaptiveCrawlPolicyRepository(MongoDbContext context)
    : MongoRepositoryBase<AdaptiveCrawlPolicy>(context.AdaptiveCrawlPolicies), IAdaptiveCrawlPolicyStore
{
    public async Task<AdaptiveCrawlPolicy?> GetAsync(string sourceName, string categoryKey, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(policy => policy.SourceName == sourceName && policy.CategoryKey == categoryKey)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertAsync(AdaptiveCrawlPolicy policy, CancellationToken cancellationToken = default)
    {
        await Collection.ReplaceOneAsync(
            existing => existing.Id == policy.Id,
            policy,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<IReadOnlyList<AdaptiveCrawlPolicy>> ListAsync(string? categoryKey = null, CancellationToken cancellationToken = default)
    {
        var filter = string.IsNullOrWhiteSpace(categoryKey)
            ? Builders<AdaptiveCrawlPolicy>.Filter.Empty
            : Builders<AdaptiveCrawlPolicy>.Filter.Eq(policy => policy.CategoryKey, categoryKey);

        return await Collection.Find(filter)
            .SortBy(policy => policy.SourceName)
            .ToListAsync(cancellationToken);
    }
}