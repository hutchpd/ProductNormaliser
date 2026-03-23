using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Discovery;

public interface IDiscoverySeedWriter
{
    Task<bool> EnqueueAsync(
        CrawlSource source,
        string categoryKey,
        string url,
        string classification,
        int depth,
        string? parentUrl,
        string? jobId,
        CancellationToken cancellationToken = default);
}