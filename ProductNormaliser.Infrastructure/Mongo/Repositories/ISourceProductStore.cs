using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public interface ISourceProductStore
{
    Task<SourceProduct?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<SourceProduct?> GetBySourceAsync(string sourceName, string sourceUrl, CancellationToken cancellationToken = default);
    Task UpsertAsync(SourceProduct product, CancellationToken cancellationToken = default);
}