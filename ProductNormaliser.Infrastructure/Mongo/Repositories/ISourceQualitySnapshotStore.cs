using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public interface ISourceQualitySnapshotStore
{
    Task InsertAsync(SourceQualitySnapshot snapshot, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SourceQualitySnapshot>> ListAsync(string categoryKey, string? sourceName = null, int limit = 100, CancellationToken cancellationToken = default);
}