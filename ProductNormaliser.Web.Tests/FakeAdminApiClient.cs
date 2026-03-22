using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Tests;

internal sealed class FakeAdminApiClient : IProductNormaliserAdminApiClient
{
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
    public CreateCrawlJobRequest? LastCreatedJobRequest { get; private set; }
    public string? LastCancelledJobId { get; private set; }
    public ProductListResponseDto ProductPage { get; set; } = new();
    public ProductDetailDto? Product { get; set; }
    public IReadOnlyList<ProductChangeEventDto> ProductHistory { get; set; } = [];
    public ProductListQueryDto? LastProductQuery { get; private set; }

    public Task<StatsDto> GetStatsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Stats);
    public Task<IReadOnlyList<CategoryMetadataDto>> GetCategoriesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Categories);
    public Task<IReadOnlyList<CategoryFamilyDto>> GetCategoryFamiliesAsync(CancellationToken cancellationToken = default) => Task.FromResult(CategoryFamilies);
    public Task<IReadOnlyList<CategoryMetadataDto>> GetEnabledCategoriesAsync(CancellationToken cancellationToken = default) => Task.FromResult(EnabledCategories);
    public Task<CategoryDetailDto?> GetCategoryDetailAsync(string categoryKey, CancellationToken cancellationToken = default) => Task.FromResult(CategoryDetail);
    public Task<IReadOnlyList<SourceDto>> GetSourcesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Sources);
    public Task<SourceDto?> GetSourceAsync(string sourceId, CancellationToken cancellationToken = default) => Task.FromResult(Source);
    public Task<SourceDto> RegisterSourceAsync(RegisterSourceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<SourceDto> UpdateSourceAsync(string sourceId, UpdateSourceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<SourceDto> EnableSourceAsync(string sourceId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<SourceDto> DisableSourceAsync(string sourceId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<SourceDto> AssignCategoriesAsync(string sourceId, AssignSourceCategoriesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<SourceDto> UpdateThrottlingAsync(string sourceId, UpdateSourceThrottlingRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<CrawlJobListResponseDto> GetCrawlJobsAsync(CrawlJobQueryDto? query = null, CancellationToken cancellationToken = default) => Task.FromResult(CrawlJobsPage);
    public Task<CrawlJobDto?> GetCrawlJobAsync(string jobId, CancellationToken cancellationToken = default) => Task.FromResult(CrawlJob);
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
    public Task<ProductDetailDto?> GetProductAsync(string productId, CancellationToken cancellationToken = default) => Task.FromResult(Product);
    public Task<IReadOnlyList<ProductChangeEventDto>> GetProductHistoryAsync(string productId, CancellationToken cancellationToken = default) => Task.FromResult(ProductHistory);
}