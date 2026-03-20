using MongoDB.Driver;
using ProductNormaliser.Application.Categories;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class CategoryMetadataRepository(MongoDbContext mongoDbContext)
    : MongoRepositoryBase<CategoryMetadata>(mongoDbContext.Categories), ICategoryMetadataStore
{
    public async Task<CategoryMetadata?> GetAsync(string categoryKey, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(category => category.CategoryKey == categoryKey)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryMetadata>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await base.ListAsync(cancellationToken: cancellationToken);
    }

    public async Task UpsertAsync(CategoryMetadata categoryMetadata, CancellationToken cancellationToken = default)
    {
        await Collection.ReplaceOneAsync(
            category => category.CategoryKey == categoryMetadata.CategoryKey,
            categoryMetadata,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }
}