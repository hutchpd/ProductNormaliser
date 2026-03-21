using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Crawls;

public interface ICrawlJobService
{
    Task<CrawlJobPage> ListAsync(CrawlJobQuery? query = null, CancellationToken cancellationToken = default);

    Task<CrawlJob?> GetAsync(string jobId, CancellationToken cancellationToken = default);

    Task<CrawlJob> CreateAsync(CreateCrawlJobRequest request, CancellationToken cancellationToken = default);

    Task<CrawlJob?> CancelAsync(string jobId, CancellationToken cancellationToken = default);

    Task MarkStartedAsync(string jobId, CancellationToken cancellationToken = default);

    Task RecordTargetOutcomeAsync(string jobId, string categoryKey, string outcome, CancellationToken cancellationToken = default);
}