using MongoDB.Driver;
using ProductNormaliser.Application.Analyst;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class AnalystNoteRepository(MongoDbContext context)
    : MongoRepositoryBase<AnalystNote>(context.AnalystNotes), IAnalystNoteStore
{
    public async Task<AnalystNote?> GetAsync(string targetType, string targetId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(note => note.TargetType == targetType && note.TargetId == targetId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertAsync(AnalystNote note, CancellationToken cancellationToken = default)
    {
        await Collection.ReplaceOneAsync(
            item => item.Id == note.Id,
            note,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task DeleteAsync(string targetType, string targetId, CancellationToken cancellationToken = default)
    {
        await Collection.DeleteOneAsync(note => note.TargetType == targetType && note.TargetId == targetId, cancellationToken);
    }
}