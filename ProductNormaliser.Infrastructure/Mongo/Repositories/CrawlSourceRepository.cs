using MongoDB.Driver;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class CrawlSourceRepository(MongoDbContext context)
    : MongoRepositoryBase<CrawlSource>(context.CrawlSources), ICrawlSourceStore
{
    public async Task<IReadOnlyList<CrawlSource>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await Collection.Find(Builders<CrawlSource>.Filter.Empty)
            .SortBy(source => source.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<CrawlSource?> GetAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(source => source.Id == sourceId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertAsync(CrawlSource source, CancellationToken cancellationToken = default)
    {
        await Collection.ReplaceOneAsync(
            existing => existing.Id == source.Id,
            source,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }
}