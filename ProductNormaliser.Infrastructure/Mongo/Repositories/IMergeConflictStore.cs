using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public interface IMergeConflictStore
{
    Task<MergeConflict?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MergeConflict>> GetByCanonicalProductIdAndStatusAsync(string canonicalProductId, string status, CancellationToken cancellationToken = default);
    Task UpsertAsync(MergeConflict conflict, CancellationToken cancellationToken = default);
}