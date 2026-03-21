using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Crawls;

public interface ICrawlJobStore
{
    Task<CrawlJobPage> ListAsync(CrawlJobQuery query, CancellationToken cancellationToken = default);

    Task<CrawlJob?> GetAsync(string jobId, CancellationToken cancellationToken = default);

    Task UpsertAsync(CrawlJob job, CancellationToken cancellationToken = default);
}