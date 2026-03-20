using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public interface ICrawlLogStore
{
    Task<CrawlLog?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CrawlLog>> ListAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task InsertAsync(CrawlLog log, CancellationToken cancellationToken = default);
}