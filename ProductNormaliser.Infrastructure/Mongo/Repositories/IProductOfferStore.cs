using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public interface IProductOfferStore
{
    Task<ProductOffer?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductOffer>> GetByCanonicalProductIdAsync(string canonicalProductId, CancellationToken cancellationToken = default);
    Task UpsertAsync(ProductOffer offer, CancellationToken cancellationToken = default);
}