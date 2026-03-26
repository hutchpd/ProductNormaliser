using MongoDB.Driver;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class DiscoveryRunRepository(MongoDbContext context)
    : MongoRepositoryBase<DiscoveryRun>(context.DiscoveryRuns), IDiscoveryRunStore
{
    public async Task<DiscoveryRunPage> ListAsync(DiscoveryRunQuery query, CancellationToken cancellationToken = default)
    {
        var filter = Builders<DiscoveryRun>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            filter &= Builders<DiscoveryRun>.Filter.Eq(run => run.Status, query.Status);
        }

        var totalCount = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await Collection.Find(filter)
            .SortByDescending(run => run.UpdatedUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Limit(query.PageSize)
            .ToListAsync(cancellationToken);

        return new DiscoveryRunPage
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<DiscoveryRun?> GetAsync(string runId, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(run => run.RunId == runId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<DiscoveryRun?> GetNextQueuedAsync(CancellationToken cancellationToken = default)
    {
        return await Collection.Find(run => run.Status == DiscoveryRunStatuses.Queued)
            .SortBy(run => run.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertAsync(DiscoveryRun run, CancellationToken cancellationToken = default)
    {
        await Collection.ReplaceOneAsync(existing => existing.RunId == run.RunId, run, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }
}