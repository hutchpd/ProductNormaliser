using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Crawling;

public interface IHttpFetcher
{
    Task<FetchResult> FetchAsync(CrawlTarget target, CancellationToken cancellationToken);
}