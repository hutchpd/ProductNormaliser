using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Crawling;

public interface IRobotsTxtCache
{
    Task<RobotsTxtSnapshot> GetAsync(CrawlTarget target, CancellationToken cancellationToken);
    Task<RobotsTxtSnapshot> GetForSourceAsync(CrawlSource source, CancellationToken cancellationToken);
}