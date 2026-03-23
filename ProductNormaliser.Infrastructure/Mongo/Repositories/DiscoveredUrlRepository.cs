using MongoDB.Driver;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class DiscoveredUrlRepository(MongoDbContext context)
    : MongoRepositoryBase<DiscoveredUrl>(context.DiscoveredUrls), IDiscoveredUrlStore
{
    public async Task<DiscoveredUrl?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(item => item.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<DiscoveredUrl?> GetByNormalizedUrlAsync(string sourceId, string categoryKey, string normalizedUrl, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(item => item.SourceId == sourceId && item.CategoryKey == categoryKey && item.NormalizedUrl == normalizedUrl)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertAsync(DiscoveredUrl item, CancellationToken cancellationToken = default)
    {
        await Collection.ReplaceOneAsync(
            existing => existing.Id == item.Id,
            item,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }
}