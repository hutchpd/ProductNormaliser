using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public interface ICanonicalProductStore
{
    Task<CanonicalProduct?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<CanonicalProduct?> GetByGtinAsync(string gtin, CancellationToken cancellationToken = default);
    Task<CanonicalProduct?> GetByBrandAndModelAsync(string brand, string modelNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CanonicalProduct>> ListPotentialMatchesAsync(string categoryKey, string? brand, CancellationToken cancellationToken = default);
    Task UpsertAsync(CanonicalProduct product, CancellationToken cancellationToken = default);
}