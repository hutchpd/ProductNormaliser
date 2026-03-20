using MongoDB.Driver;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class SourceProductRepository(MongoDbContext context) : MongoRepositoryBase<SourceProduct>(context.SourceProducts), ISourceProductStore
{
    public async Task<SourceProduct?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(product => product.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SourceProduct?> GetBySourceAsync(string sourceName, string sourceUrl, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(product => product.SourceName == sourceName && product.SourceUrl == sourceUrl)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertAsync(SourceProduct product, CancellationToken cancellationToken = default)
    {
        await Collection.ReplaceOneAsync(
            existingProduct => existingProduct.Id == product.Id,
            product,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }
}