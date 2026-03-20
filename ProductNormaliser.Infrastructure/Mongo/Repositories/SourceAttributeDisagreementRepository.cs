using MongoDB.Driver;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class SourceAttributeDisagreementRepository(MongoDbContext context)
    : MongoRepositoryBase<SourceAttributeDisagreement>(context.SourceAttributeDisagreements), ISourceAttributeDisagreementStore
{
    public async Task<SourceAttributeDisagreement?> GetAsync(string sourceName, string categoryKey, string attributeKey, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(disagreement => disagreement.SourceName == sourceName && disagreement.CategoryKey == categoryKey && disagreement.AttributeKey == attributeKey)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertAsync(SourceAttributeDisagreement disagreement, CancellationToken cancellationToken = default)
    {
        await Collection.ReplaceOneAsync(
            existing => existing.Id == disagreement.Id,
            disagreement,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<IReadOnlyList<SourceAttributeDisagreement>> ListAsync(string categoryKey, string? sourceName = null, CancellationToken cancellationToken = default)
    {
        var filter = Builders<SourceAttributeDisagreement>.Filter.Eq(item => item.CategoryKey, categoryKey);
        if (!string.IsNullOrWhiteSpace(sourceName))
        {
            filter &= Builders<SourceAttributeDisagreement>.Filter.Eq(item => item.SourceName, sourceName);
        }

        return await Collection.Find(filter)
            .SortByDescending(item => item.DisagreementRate)
            .ThenBy(item => item.SourceName)
            .ToListAsync(cancellationToken);
    }
}