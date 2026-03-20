using MongoDB.Driver;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class CrawlLogRepository(MongoDbContext context) : MongoRepositoryBase<CrawlLog>(context.CrawlLogs), ICrawlLogStore
{
    public async Task<CrawlLog?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(log => log.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CrawlLog>> ListAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(Builders<CrawlLog>.Filter.Empty)
            .SortByDescending(log => log.TimestampUtc)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

    public override async Task InsertAsync(CrawlLog document, CancellationToken cancellationToken = default)
    {
        await base.InsertAsync(document, cancellationToken);
    }
}