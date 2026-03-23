using ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Services;

public interface IProductNormaliserAdminApiClient
{
    Task<StatsDto> GetStatsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryMetadataDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryFamilyDto>> GetCategoryFamiliesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryMetadataDto>> GetEnabledCategoriesAsync(CancellationToken cancellationToken = default);

    Task<CategoryDetailDto?> GetCategoryDetailAsync(string categoryKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SourceDto>> GetSourcesAsync(CancellationToken cancellationToken = default);

    Task<SourceDto?> GetSourceAsync(string sourceId, CancellationToken cancellationToken = default);

    Task<SourceDto> RegisterSourceAsync(RegisterSourceRequest request, CancellationToken cancellationToken = default);

    Task<SourceDto> UpdateSourceAsync(string sourceId, UpdateSourceRequest request, CancellationToken cancellationToken = default);

    Task<SourceDto> EnableSourceAsync(string sourceId, CancellationToken cancellationToken = default);

    Task<SourceDto> DisableSourceAsync(string sourceId, CancellationToken cancellationToken = default);

    Task<SourceDto> AssignCategoriesAsync(string sourceId, AssignSourceCategoriesRequest request, CancellationToken cancellationToken = default);

    Task<SourceDto> UpdateThrottlingAsync(string sourceId, UpdateSourceThrottlingRequest request, CancellationToken cancellationToken = default);

    Task<CrawlJobListResponseDto> GetCrawlJobsAsync(CrawlJobQueryDto? query = null, CancellationToken cancellationToken = default);

    Task<CrawlJobDto?> GetCrawlJobAsync(string jobId, CancellationToken cancellationToken = default);

    Task<CrawlJobDto> CreateCrawlJobAsync(CreateCrawlJobRequest request, CancellationToken cancellationToken = default);

    Task<CrawlJobDto> CancelCrawlJobAsync(string jobId, CancellationToken cancellationToken = default);

    Task<ProductListResponseDto> GetProductsAsync(ProductListQueryDto? query = null, CancellationToken cancellationToken = default);

    Task<ProductDetailDto?> GetProductAsync(string productId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductChangeEventDto>> GetProductHistoryAsync(string productId, CancellationToken cancellationToken = default);

    Task<DetailedCoverageResponseDto> GetDetailedCoverageAsync(string categoryKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UnmappedAttributeDto>> GetUnmappedAttributesAsync(string categoryKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SourceQualityScoreDto>> GetSourceQualityScoresAsync(string categoryKey, CancellationToken cancellationToken = default);

    Task<MergeInsightsResponseDto> GetMergeInsightsAsync(string categoryKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SourceQualitySnapshotDto>> GetSourceHistoryAsync(string categoryKey, string? sourceName = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttributeStabilityDto>> GetAttributeStabilityAsync(string categoryKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SourceAttributeDisagreementDto>> GetSourceDisagreementsAsync(string categoryKey, string? sourceName = null, CancellationToken cancellationToken = default);
}