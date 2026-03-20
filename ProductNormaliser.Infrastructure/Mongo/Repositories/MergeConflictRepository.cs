using MongoDB.Driver;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class MergeConflictRepository(MongoDbContext context) : MongoRepositoryBase<MergeConflict>(context.MergeConflicts)
{
    public async Task<MergeConflict?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(conflict => conflict.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MergeConflict>> GetByCanonicalProductIdAndStatusAsync(
        string canonicalProductId,
        string status,
        CancellationToken cancellationToken = default)
    {
        var cursor = await Collection.FindAsync(
            conflict => conflict.CanonicalProductId == canonicalProductId && conflict.Status == status,
            cancellationToken: cancellationToken);

        return await cursor.ToListAsync(cancellationToken);
    }

    public async Task UpsertAsync(MergeConflict conflict, CancellationToken cancellationToken = default)
    {
        await Collection.ReplaceOneAsync(
            existingConflict => existingConflict.Id == conflict.Id,
            conflict,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }
}