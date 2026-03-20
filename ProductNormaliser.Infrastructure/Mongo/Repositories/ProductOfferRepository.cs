using MongoDB.Driver;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class ProductOfferRepository(MongoDbContext context) : MongoRepositoryBase<ProductOffer>(context.ProductOffers)
{
    public async Task<ProductOffer?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(offer => offer.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductOffer>> GetByCanonicalProductIdAsync(string canonicalProductId, CancellationToken cancellationToken = default)
    {
        var cursor = await Collection.FindAsync(offer => offer.CanonicalProductId == canonicalProductId, cancellationToken: cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }

    public async Task UpsertAsync(ProductOffer offer, CancellationToken cancellationToken = default)
    {
        await Collection.ReplaceOneAsync(
            existingOffer => existingOffer.Id == offer.Id,
            offer,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }
}