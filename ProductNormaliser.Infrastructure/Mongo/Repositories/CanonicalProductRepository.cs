using MongoDB.Driver;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class CanonicalProductRepository(MongoDbContext context) : MongoRepositoryBase<CanonicalProduct>(context.CanonicalProducts)
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

    public async Task UpsertAsync(CanonicalProduct product, CancellationToken cancellationToken = default)
    {
        await Collection.ReplaceOneAsync(
            existingProduct => existingProduct.Id == product.Id,
            product,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }
}