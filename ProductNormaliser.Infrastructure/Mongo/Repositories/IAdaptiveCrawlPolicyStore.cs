using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public interface IAdaptiveCrawlPolicyStore
{
    Task<AdaptiveCrawlPolicy?> GetAsync(string sourceName, string categoryKey, CancellationToken cancellationToken = default);
    Task UpsertAsync(AdaptiveCrawlPolicy policy, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdaptiveCrawlPolicy>> ListAsync(string? categoryKey = null, CancellationToken cancellationToken = default);
}