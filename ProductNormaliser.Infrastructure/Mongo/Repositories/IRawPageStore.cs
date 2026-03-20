using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public interface IRawPageStore
{
    Task<RawPage?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<RawPage?> GetLatestBySourceAsync(string sourceName, string sourceUrl, CancellationToken cancellationToken = default);
    Task UpsertAsync(RawPage page, CancellationToken cancellationToken = default);
}