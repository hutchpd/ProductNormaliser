using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public interface IProductChangeEventStore
{
    Task InsertManyAsync(IReadOnlyCollection<ProductChangeEvent> changeEvents, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductChangeEvent>> GetByCanonicalProductIdAsync(string canonicalProductId, int limit = 100, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductChangeEvent>> ListByCategoryAsync(string categoryKey, int limit = 500, CancellationToken cancellationToken = default);
}