using MongoDB.Driver;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class SourceQualitySnapshotRepository(MongoDbContext context)
    : MongoRepositoryBase<SourceQualitySnapshot>(context.SourceQualitySnapshots), ISourceQualitySnapshotStore
{
    public async Task<IReadOnlyList<SourceQualitySnapshot>> ListAsync(string categoryKey, string? sourceName = null, int limit = 100, CancellationToken cancellationToken = default)
    {
        var filter = Builders<SourceQualitySnapshot>.Filter.Eq(snapshot => snapshot.CategoryKey, categoryKey);
        if (!string.IsNullOrWhiteSpace(sourceName))
        {
            filter &= Builders<SourceQualitySnapshot>.Filter.Eq(snapshot => snapshot.SourceName, sourceName);
        }

        return await Collection.Find(filter)
            .SortByDescending(snapshot => snapshot.TimestampUtc)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }
}