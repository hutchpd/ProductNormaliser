using ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Services;

public interface IProductNormaliserAdminApiClient
{
    Task<StatsDto> GetStatsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryMetadataDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AnalystWorkflowDto>> GetAnalystWorkflowsAsync(string? workflowType = null, string? routePath = null, CancellationToken cancellationToken = default);

    Task<AnalystWorkflowDto?> GetAnalystWorkflowAsync(string workflowId, CancellationToken cancellationToken = default);

    Task<AnalystWorkflowDto> SaveAnalystWorkflowAsync(UpsertAnalystWorkflowRequest request, CancellationToken cancellationToken = default);

    Task DeleteAnalystWorkflowAsync(string workflowId, CancellationToken cancellationToken = default);

    Task<AnalystNoteDto?> GetAnalystNoteAsync(string targetType, string targetId, CancellationToken cancellationToken = default);

    Task<AnalystNoteDto> SaveAnalystNoteAsync(UpsertAnalystNoteRequest request, CancellationToken cancellationToken = default);

    Task DeleteAnalystNoteAsync(string targetType, string targetId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryFamilyDto>> GetCategoryFamiliesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryMetadataDto>> GetEnabledCategoriesAsync(CancellationToken cancellationToken = default);

    Task<CategoryDetailDto?> GetCategoryDetailAsync(string categoryKey, CancellationToken cancellationToken = default);

    Task<CategorySchemaDto> UpdateCategorySchemaAsync(string categoryKey, UpdateCategorySchemaRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SourceDto>> GetSourcesAsync(CancellationToken cancellationToken = default);

    Task<SourceOnboardingAutomationSettingsDto> GetSourceOnboardingAutomationSettingsAsync(CancellationToken cancellationToken = default);

    Task<SourceDto?> GetSourceAsync(string sourceId, CancellationToken cancellationToken = default);

    Task<SourceDto> RegisterSourceAsync(RegisterSourceRequest request, CancellationToken cancellationToken = default);

    Task<SourceDto> UpdateSourceAsync(string sourceId, UpdateSourceRequest request, CancellationToken cancellationToken = default);

    Task<SourceDto> EnableSourceAsync(string sourceId, CancellationToken cancellationToken = default);

    Task<SourceDto> DisableSourceAsync(string sourceId, CancellationToken cancellationToken = default);

    Task<SourceDto> AssignCategoriesAsync(string sourceId, AssignSourceCategoriesRequest request, CancellationToken cancellationToken = default);

    Task<SourceDto> UpdateThrottlingAsync(string sourceId, UpdateSourceThrottlingRequest request, CancellationToken cancellationToken = default);

    Task<SourceCandidateDiscoveryResponseDto> DiscoverSourceCandidatesAsync(DiscoverSourceCandidatesRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecurringDiscoveryCampaignDto>> GetRecurringDiscoveryCampaignsAsync(string? status = null, CancellationToken cancellationToken = default);

    Task<RecurringDiscoveryCampaignDto?> GetRecurringDiscoveryCampaignAsync(string campaignId, CancellationToken cancellationToken = default);

    Task<RecurringDiscoveryCampaignDto> CreateRecurringDiscoveryCampaignAsync(CreateRecurringDiscoveryCampaignRequest request, CancellationToken cancellationToken = default);

    Task<RecurringDiscoveryCampaignDto> UpdateRecurringDiscoveryCampaignConfigurationAsync(string campaignId, UpdateRecurringDiscoveryCampaignConfigurationRequest request, CancellationToken cancellationToken = default);

    Task<RecurringDiscoveryCampaignDto> PauseRecurringDiscoveryCampaignAsync(string campaignId, CancellationToken cancellationToken = default);

    Task<RecurringDiscoveryCampaignDto> ResumeRecurringDiscoveryCampaignAsync(string campaignId, CancellationToken cancellationToken = default);

    Task DeleteRecurringDiscoveryCampaignAsync(string campaignId, CancellationToken cancellationToken = default);

    Task<DiscoveryRunDto> CreateDiscoveryRunAsync(CreateDiscoveryRunRequest request, CancellationToken cancellationToken = default);

    Task<DiscoveryRunPageDto> GetDiscoveryRunsAsync(string? status = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);

    Task<DiscoveryRunDto?> GetDiscoveryRunAsync(string runId, CancellationToken cancellationToken = default);

    Task<DiscoveryRunCandidatePageDto> GetDiscoveryRunCandidatesAsync(string runId, DiscoveryRunCandidateQueryDto? query = null, CancellationToken cancellationToken = default);

    Task<DiscoveryRunDto> PauseDiscoveryRunAsync(string runId, CancellationToken cancellationToken = default);

    Task<DiscoveryRunDto> ResumeDiscoveryRunAsync(string runId, CancellationToken cancellationToken = default);

    Task<DiscoveryRunDto> StopDiscoveryRunAsync(string runId, CancellationToken cancellationToken = default);

    Task<DiscoveryRunCandidateDto> AcceptDiscoveryRunCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default);

    Task<DiscoveryRunCandidateDto> DismissDiscoveryRunCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default);

    Task<DiscoveryRunCandidateDto> RestoreDiscoveryRunCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default);

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

    Task<IReadOnlyList<SourceQualitySnapshotDto>> GetSourceHistoryAsync(string categoryKey, string? sourceName = null, int? timeRangeDays = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttributeStabilityDto>> GetAttributeStabilityAsync(string categoryKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SourceAttributeDisagreementDto>> GetSourceDisagreementsAsync(string categoryKey, string? sourceName = null, int? timeRangeDays = null, CancellationToken cancellationToken = default);
}