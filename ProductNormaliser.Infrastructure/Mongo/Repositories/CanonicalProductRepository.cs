using MongoDB.Driver;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class CanonicalProductRepository(MongoDbContext context) : MongoRepositoryBase<CanonicalProduct>(context.CanonicalProducts), ICanonicalProductStore
{
    public async Task<CanonicalProduct?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(product => product.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CanonicalProduct?> GetByGtinAsync(string gtin, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(product => product.Gtin == gtin).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CanonicalProduct?> GetByBrandAndModelAsync(string brand, string modelNumber, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(product => product.Brand == brand && product.ModelNumber == modelNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CanonicalProduct>> ListPotentialMatchesAsync(string categoryKey, string? brand, CancellationToken cancellationToken = default)
    {
        var filter = Builders<CanonicalProduct>.Filter.Eq(product => product.CategoryKey, categoryKey);
        if (!string.IsNullOrWhiteSpace(brand))
        {
            filter &= Builders<CanonicalProduct>.Filter.Eq(product => product.Brand, brand);
        }

        var cursor = await Collection.FindAsync(filter, cancellationToken: cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }

    public async Task UpsertAsync(CanonicalProduct product, CancellationToken cancellationToken = default)
    {
        await Collection.ReplaceOneAsync(
            existingProduct => existingProduct.Id == product.Id,
            product,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }
}