using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

public interface ISourceManagementService
{
    Task<IReadOnlyList<CrawlSource>> ListAsync(CancellationToken cancellationToken = default);
    Task<CrawlSource?> GetAsync(string sourceId, CancellationToken cancellationToken = default);
    Task<CrawlSource> RegisterAsync(CrawlSourceRegistration registration, CancellationToken cancellationToken = default);
    Task<CrawlSource> UpdateAsync(string sourceId, CrawlSourceUpdate update, CancellationToken cancellationToken = default);
    Task<CrawlSource> EnableAsync(string sourceId, CancellationToken cancellationToken = default);
    Task<CrawlSource> DisableAsync(string sourceId, CancellationToken cancellationToken = default);
    Task<CrawlSource> AssignCategoriesAsync(string sourceId, IReadOnlyCollection<string> categoryKeys, CancellationToken cancellationToken = default);
    Task<CrawlSource> SetThrottlingAsync(string sourceId, SourceThrottlingPolicy policy, CancellationToken cancellationToken = default);
}