using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Discovery;

public interface ISitemapLocator
{
    Task<IReadOnlyList<string>> LocateAsync(CrawlSource source, CancellationToken cancellationToken = default);
}