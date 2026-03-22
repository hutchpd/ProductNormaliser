using ProductNormaliser.AdminApi.Contracts;

namespace ProductNormaliser.AdminApi.Services;

public interface IAdminQueryService
{
    Task<IReadOnlyList<CrawlLogDto>> GetCrawlLogsAsync(CancellationToken cancellationToken);
    Task<CrawlLogDto?> GetCrawlLogAsync(string id, CancellationToken cancellationToken);
    Task<IReadOnlyList<QueueItemDto>> GetQueueAsync(CancellationToken cancellationToken);
    Task<ProductListResponse> ListProductsAsync(string? categoryKey, string? search, int? minSourceCount, string? freshness, string? conflictStatus, string? completenessStatus, int page, int pageSize, CancellationToken cancellationToken);
    Task<ProductDetailResponse?> GetProductAsync(string id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductChangeEventDto>> GetProductHistoryAsync(string id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConflictDto>> GetConflictsAsync(CancellationToken cancellationToken);
    Task<StatsResponse> GetStatsAsync(CancellationToken cancellationToken);
}