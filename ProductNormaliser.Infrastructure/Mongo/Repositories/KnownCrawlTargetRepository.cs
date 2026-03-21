using MongoDB.Driver;
using ProductNormaliser.Application.Crawls;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class KnownCrawlTargetRepository(MongoDbContext context) : IKnownCrawlTargetStore
{
    public async Task<IReadOnlyList<CrawlJobTargetDescriptor>> ListKnownTargetsAsync(
        IReadOnlyCollection<string> categoryKeys,
        IReadOnlyCollection<string> sourceNames,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<SourceProduct>.Filter.Empty;
        if (categoryKeys.Count > 0)
        {
            filter &= Builders<SourceProduct>.Filter.In(product => product.CategoryKey, categoryKeys);
        }

        if (sourceNames.Count > 0)
        {
            filter &= Builders<SourceProduct>.Filter.In(product => product.SourceName, sourceNames);
        }

        var products = await context.SourceProducts
            .Find(filter)
            .Project(product => new CrawlJobTargetDescriptor
            {
                SourceName = product.SourceName,
                SourceUrl = product.SourceUrl,
                CategoryKey = product.CategoryKey
            })
            .ToListAsync(cancellationToken);

        return products;
    }

    public async Task<IReadOnlyList<CrawlJobTargetDescriptor>> ListTargetsForProductsAsync(
        IReadOnlyCollection<string> productIds,
        CancellationToken cancellationToken = default)
    {
        var products = await context.CanonicalProducts
            .Find(Builders<CanonicalProduct>.Filter.In(product => product.Id, productIds))
            .ToListAsync(cancellationToken);

        return products
            .SelectMany(product => product.Sources.Select(source => new CrawlJobTargetDescriptor
            {
                SourceName = source.SourceName,
                SourceUrl = source.SourceUrl,
                CategoryKey = product.CategoryKey
            }))
            .ToArray();
    }
}