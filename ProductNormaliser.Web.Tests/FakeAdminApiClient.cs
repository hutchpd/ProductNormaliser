using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Tests;

internal sealed class FakeAdminApiClient : IProductNormaliserAdminApiClient
{
    public Exception? StatsException { get; set; }
    public Exception? CategoriesException { get; set; }
    public Exception? CategoryDetailException { get; set; }
    public Exception? SourcesException { get; set; }
    public Exception? CrawlJobsException { get; set; }
    public StatsDto Stats { get; set; } = new();
    public IReadOnlyList<CategoryMetadataDto> Categories { get; set; } = [];
    public IReadOnlyList<CategoryFamilyDto> CategoryFamilies { get; set; } = [];
    public IReadOnlyList<CategoryMetadataDto> EnabledCategories { get; set; } = [];
    public CategoryDetailDto? CategoryDetail { get; set; }
    public IReadOnlyList<SourceDto> Sources { get; set; } = [];
    public SourceDto? Source { get; set; }
    public CrawlJobListResponseDto CrawlJobsPage { get; set; } = new();
    public CrawlJobDto? CrawlJob { get; set; }
    public CrawlJobDto? CreatedJob { get; set; }
    public CrawlJobDto? CancelledJob { get; set; }
    public Exception? CreateJobException { get; set; }
    public Exception? AnalyticsException { get; set; }
    public CreateCrawlJobRequest? LastCreatedJobRequest { get; private set; }
    public string? LastCancelledJobId { get; private set; }
    public string? LastRequestedCrawlJobId { get; private set; }
    public ProductListResponseDto ProductPage { get; set; } = new();
    public ProductDetailDto? Product { get; set; }
    public IReadOnlyList<ProductChangeEventDto> ProductHistory { get; set; } = [];
    public ProductListQueryDto? LastProductQuery { get; private set; }
    public string? LastRequestedProductId { get; private set; }
    public string? LastRequestedProductHistoryId { get; private set; }
    public DetailedCoverageResponseDto DetailedCoverage { get; set; } = new();
    public IReadOnlyList<UnmappedAttributeDto> UnmappedAttributes { get; set; } = [];
    public IReadOnlyList<SourceQualityScoreDto> SourceQualityScores { get; set; } = [];
    public MergeInsightsResponseDto MergeInsights { get; set; } = new();
    public IReadOnlyList<SourceQualitySnapshotDto> SourceHistory { get; set; } = [];
    public IReadOnlyList<AttributeStabilityDto> AttributeStability { get; set; } = [];
    public IReadOnlyList<SourceAttributeDisagreementDto> SourceDisagreements { get; set; } = [];
    public string? LastCoverageCategoryKey { get; private set; }
    public string? LastUnmappedCategoryKey { get; private set; }
    public string? LastSourceQualityCategoryKey { get; private set; }
    public string? LastMergeInsightsCategoryKey { get; private set; }
    public string? LastSourceHistoryCategoryKey { get; private set; }
    public string? LastSourceHistorySourceName { get; private set; }
    public string? LastAttributeStabilityCategoryKey { get; private set; }
    public string? LastSourceDisagreementsCategoryKey { get; private set; }
    public string? LastSourceDisagreementsSourceName { get; private set; }

    public Task<StatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
        => StatsException is null ? Task.FromResult(Stats) : Task.FromException<StatsDto>(StatsException);

    public Task<IReadOnlyList<CategoryMetadataDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        => CategoriesException is null ? Task.FromResult(Categories) : Task.FromException<IReadOnlyList<CategoryMetadataDto>>(CategoriesException);
    public Task<IReadOnlyList<CategoryFamilyDto>> GetCategoryFamiliesAsync(CancellationToken cancellationToken = default) => Task.FromResult(CategoryFamilies);
    public Task<IReadOnlyList<CategoryMetadataDto>> GetEnabledCategoriesAsync(CancellationToken cancellationToken = default) => Task.FromResult(EnabledCategories);
    public Task<CategoryDetailDto?> GetCategoryDetailAsync(string categoryKey, CancellationToken cancellationToken = default)
        => CategoryDetailException is null ? Task.FromResult(CategoryDetail) : Task.FromException<CategoryDetailDto?>(CategoryDetailException);

    public Task<IReadOnlyList<SourceDto>> GetSourcesAsync(CancellationToken cancellationToken = default)
        => SourcesException is null ? Task.FromResult(Sources) : Task.FromException<IReadOnlyList<SourceDto>>(SourcesException);
    public Task<SourceDto?> GetSourceAsync(string sourceId, CancellationToken cancellationToken = default) => Task.FromResult(Source);
    public Task<SourceDto> RegisterSourceAsync(RegisterSourceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<SourceDto> UpdateSourceAsync(string sourceId, UpdateSourceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<SourceDto> EnableSourceAsync(string sourceId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<SourceDto> DisableSourceAsync(string sourceId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<SourceDto> AssignCategoriesAsync(string sourceId, AssignSourceCategoriesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<SourceDto> UpdateThrottlingAsync(string sourceId, UpdateSourceThrottlingRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<CrawlJobListResponseDto> GetCrawlJobsAsync(CrawlJobQueryDto? query = null, CancellationToken cancellationToken = default)
        => CrawlJobsException is null ? Task.FromResult(CrawlJobsPage) : Task.FromException<CrawlJobListResponseDto>(CrawlJobsException);
    public Task<CrawlJobDto?> GetCrawlJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        LastRequestedCrawlJobId = jobId;
        return Task.FromResult(CrawlJob);
    }
    public Task<CrawlJobDto> CreateCrawlJobAsync(CreateCrawlJobRequest request, CancellationToken cancellationToken = default)
    {
        LastCreatedJobRequest = request;
        if (CreateJobException is not null)
        {
            return Task.FromException<CrawlJobDto>(CreateJobException);
        }

        return Task.FromResult(CreatedJob ?? new CrawlJobDto { JobId = "job_default" });
    }

    public Task<CrawlJobDto> CancelCrawlJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        LastCancelledJobId = jobId;
        return Task.FromResult(CancelledJob ?? new CrawlJobDto { JobId = jobId, Status = "cancel_requested" });
    }

    public Task<ProductListResponseDto> GetProductsAsync(ProductListQueryDto? query = null, CancellationToken cancellationToken = default)
    {
        LastProductQuery = query;
        return Task.FromResult(ProductPage);
    }
    public Task<ProductDetailDto?> GetProductAsync(string productId, CancellationToken cancellationToken = default)
    {
        LastRequestedProductId = productId;
        return Task.FromResult(Product);
    }

    public Task<IReadOnlyList<ProductChangeEventDto>> GetProductHistoryAsync(string productId, CancellationToken cancellationToken = default)
    {
        LastRequestedProductHistoryId = productId;
        return Task.FromResult(ProductHistory);
    }

    public Task<DetailedCoverageResponseDto> GetDetailedCoverageAsync(string categoryKey, CancellationToken cancellationToken = default)
    {
        if (AnalyticsException is not null)
        {
            return Task.FromException<DetailedCoverageResponseDto>(AnalyticsException);
        }

        LastCoverageCategoryKey = categoryKey;
        return Task.FromResult(DetailedCoverage);
    }

    public Task<IReadOnlyList<UnmappedAttributeDto>> GetUnmappedAttributesAsync(string categoryKey, CancellationToken cancellationToken = default)
    {
        if (AnalyticsException is not null)
        {
            return Task.FromException<IReadOnlyList<UnmappedAttributeDto>>(AnalyticsException);
        }

        LastUnmappedCategoryKey = categoryKey;
        return Task.FromResult(UnmappedAttributes);
    }

    public Task<IReadOnlyList<SourceQualityScoreDto>> GetSourceQualityScoresAsync(string categoryKey, CancellationToken cancellationToken = default)
    {
        if (AnalyticsException is not null)
        {
            return Task.FromException<IReadOnlyList<SourceQualityScoreDto>>(AnalyticsException);
        }

        LastSourceQualityCategoryKey = categoryKey;
        return Task.FromResult(SourceQualityScores);
    }

    public Task<MergeInsightsResponseDto> GetMergeInsightsAsync(string categoryKey, CancellationToken cancellationToken = default)
    {
        if (AnalyticsException is not null)
        {
            return Task.FromException<MergeInsightsResponseDto>(AnalyticsException);
        }

        LastMergeInsightsCategoryKey = categoryKey;
        return Task.FromResult(MergeInsights);
    }

    public Task<IReadOnlyList<SourceQualitySnapshotDto>> GetSourceHistoryAsync(string categoryKey, string? sourceName = null, CancellationToken cancellationToken = default)
    {
        if (AnalyticsException is not null)
        {
            return Task.FromException<IReadOnlyList<SourceQualitySnapshotDto>>(AnalyticsException);
        }

        LastSourceHistoryCategoryKey = categoryKey;
        LastSourceHistorySourceName = sourceName;
        return Task.FromResult(SourceHistory);
    }

    public Task<IReadOnlyList<AttributeStabilityDto>> GetAttributeStabilityAsync(string categoryKey, CancellationToken cancellationToken = default)
    {
        if (AnalyticsException is not null)
        {
            return Task.FromException<IReadOnlyList<AttributeStabilityDto>>(AnalyticsException);
        }

        LastAttributeStabilityCategoryKey = categoryKey;
        return Task.FromResult(AttributeStability);
    }

    public Task<IReadOnlyList<SourceAttributeDisagreementDto>> GetSourceDisagreementsAsync(string categoryKey, string? sourceName = null, CancellationToken cancellationToken = default)
    {
        if (AnalyticsException is not null)
        {
            return Task.FromException<IReadOnlyList<SourceAttributeDisagreementDto>>(AnalyticsException);
        }

        LastSourceDisagreementsCategoryKey = categoryKey;
        LastSourceDisagreementsSourceName = sourceName;
        return Task.FromResult(SourceDisagreements);
    }
}