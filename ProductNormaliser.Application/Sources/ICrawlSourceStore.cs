using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

public interface ICrawlSourceStore
{
    Task<IReadOnlyList<CrawlSource>> ListAsync(CancellationToken cancellationToken = default);
    Task<CrawlSource?> GetAsync(string sourceId, CancellationToken cancellationToken = default);
    Task UpsertAsync(CrawlSource source, CancellationToken cancellationToken = default);
}