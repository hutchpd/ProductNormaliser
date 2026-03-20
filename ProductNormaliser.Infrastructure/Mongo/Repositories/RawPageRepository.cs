using MongoDB.Driver;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class RawPageRepository(MongoDbContext context) : MongoRepositoryBase<RawPage>(context.RawPages)
{
    public async Task<RawPage?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(page => page.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertAsync(RawPage page, CancellationToken cancellationToken = default)
    {
        await Collection.ReplaceOneAsync(
            existingPage => existingPage.Id == page.Id,
            page,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }
}