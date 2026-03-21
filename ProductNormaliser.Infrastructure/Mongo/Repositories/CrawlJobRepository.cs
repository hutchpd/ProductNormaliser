using MongoDB.Driver;
using ProductNormaliser.Application.Crawls;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class CrawlJobRepository(MongoDbContext context)
    : MongoRepositoryBase<CrawlJob>(context.CrawlJobs), ICrawlJobStore
{
    public async Task<CrawlJobPage> ListAsync(CrawlJobQuery query, CancellationToken cancellationToken = default)
    {
        var filter = Builders<CrawlJob>.Filter.Empty;

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            filter &= Builders<CrawlJob>.Filter.Eq(job => job.Status, query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.RequestType))
        {
            filter &= Builders<CrawlJob>.Filter.Eq(job => job.RequestType, query.RequestType);
        }

        if (!string.IsNullOrWhiteSpace(query.CategoryKey))
        {
            filter &= Builders<CrawlJob>.Filter.Or(
                Builders<CrawlJob>.Filter.AnyEq(job => job.RequestedCategories, query.CategoryKey),
                Builders<CrawlJob>.Filter.ElemMatch(job => job.PerCategoryBreakdown, item => item.CategoryKey == query.CategoryKey));
        }

        var totalCount = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await Collection.Find(filter)
            .SortByDescending(job => job.LastUpdatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Limit(query.PageSize)
            .ToListAsync(cancellationToken);

        return new CrawlJobPage
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<CrawlJob?> GetAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(job => job.JobId == jobId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertAsync(CrawlJob job, CancellationToken cancellationToken = default)
    {
        await Collection.ReplaceOneAsync(
            existing => existing.JobId == job.JobId,
            job,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }
}