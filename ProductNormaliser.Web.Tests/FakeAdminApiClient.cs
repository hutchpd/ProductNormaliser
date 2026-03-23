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
    public UpdateSourceRequest? LastUpdatedSourceRequest { get; private set; }
    public string? LastUpdatedSourceId { get; private set; }
    public string? LastEnabledSourceId { get; private set; }
    public string? LastDisabledSourceId { get; private set; }
    public AssignSourceCategoriesRequest? LastAssignedCategoriesRequest { get; private set; }
    public string? LastAssignedCategoriesSourceId { get; private set; }
    public UpdateSourceThrottlingRequest? LastUpdatedThrottlingRequest { get; private set; }
    public string? LastUpdatedThrottlingSourceId { get; private set; }
    public CrawlJobListResponseDto CrawlJobsPage { get; set; } = new();
    public CrawlJobDto? CrawlJob { get; set; }
    public CrawlJobDto? CreatedJob { get; set; }
    public CrawlJobDto? CancelledJob { get; set; }
    public Exception? CreateJobException { get; set; }
    public Exception? AnalyticsException { get; set; }
    public Exception? AnalystWorkspaceException { get; set; }
    public CreateCrawlJobRequest? LastCreatedJobRequest { get; private set; }
    public string? LastCancelledJobId { get; private set; }
    public string? LastRequestedCrawlJobId { get; private set; }
    public ProductListResponseDto ProductPage { get; set; } = new();
    public ProductDetailDto? Product { get; set; }
    public IReadOnlyList<ProductChangeEventDto> ProductHistory { get; set; } = [];
    public IReadOnlyList<AnalystWorkflowDto> AnalystWorkflows { get; set; } = [];
    public AnalystWorkflowDto? SavedAnalystWorkflow { get; set; }
    public AnalystNoteDto? AnalystNote { get; set; }
    public UpsertAnalystWorkflowRequest? LastSavedAnalystWorkflowRequest { get; private set; }
    public string? LastDeletedAnalystWorkflowId { get; private set; }
    public string? LastRequestedAnalystWorkflowId { get; private set; }
    public string? LastRequestedWorkflowType { get; private set; }
    public string? LastRequestedWorkflowRoutePath { get; private set; }
    public UpsertAnalystNoteRequest? LastSavedAnalystNoteRequest { get; private set; }
    public string? LastDeletedAnalystNoteTargetType { get; private set; }
    public string? LastDeletedAnalystNoteTargetId { get; private set; }
    public string? LastRequestedAnalystNoteTargetType { get; private set; }
    public string? LastRequestedAnalystNoteTargetId { get; private set; }
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
    public int? LastSourceHistoryTimeRangeDays { get; private set; }
    public string? LastAttributeStabilityCategoryKey { get; private set; }
    public string? LastSourceDisagreementsCategoryKey { get; private set; }
    public string? LastSourceDisagreementsSourceName { get; private set; }
    public int? LastSourceDisagreementsTimeRangeDays { get; private set; }

    public Task<StatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
        => StatsException is null ? Task.FromResult(Stats) : Task.FromException<StatsDto>(StatsException);

    public Task<IReadOnlyList<CategoryMetadataDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        => CategoriesException is null ? Task.FromResult(Categories) : Task.FromException<IReadOnlyList<CategoryMetadataDto>>(CategoriesException);
    public Task<IReadOnlyList<AnalystWorkflowDto>> GetAnalystWorkflowsAsync(string? workflowType = null, string? routePath = null, CancellationToken cancellationToken = default)
    {
        if (AnalystWorkspaceException is not null)
        {
            return Task.FromException<IReadOnlyList<AnalystWorkflowDto>>(AnalystWorkspaceException);
        }

        LastRequestedWorkflowType = workflowType;
        LastRequestedWorkflowRoutePath = routePath;

        IEnumerable<AnalystWorkflowDto> workflows = AnalystWorkflows;
        if (!string.IsNullOrWhiteSpace(workflowType))
        {
            workflows = workflows.Where(item => string.Equals(item.WorkflowType, workflowType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(routePath))
        {
            workflows = workflows.Where(item => string.Equals(item.RoutePath, routePath, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult<IReadOnlyList<AnalystWorkflowDto>>(workflows.ToArray());
    }
    public Task<AnalystWorkflowDto?> GetAnalystWorkflowAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        if (AnalystWorkspaceException is not null)
        {
            return Task.FromException<AnalystWorkflowDto?>(AnalystWorkspaceException);
        }

        LastRequestedAnalystWorkflowId = workflowId;
        return Task.FromResult(SavedAnalystWorkflow ?? AnalystWorkflows.FirstOrDefault(item => string.Equals(item.Id, workflowId, StringComparison.OrdinalIgnoreCase)));
    }
    public Task<AnalystWorkflowDto> SaveAnalystWorkflowAsync(UpsertAnalystWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        if (AnalystWorkspaceException is not null)
        {
            return Task.FromException<AnalystWorkflowDto>(AnalystWorkspaceException);
        }

        LastSavedAnalystWorkflowRequest = request;
        var workflow = SavedAnalystWorkflow ?? new AnalystWorkflowDto
        {
            Id = string.IsNullOrWhiteSpace(request.Id) ? "workflow_saved" : request.Id,
            Name = request.Name,
            WorkflowType = request.WorkflowType,
            RoutePath = request.RoutePath,
            Description = request.Description,
            PrimaryCategoryKey = request.PrimaryCategoryKey,
            SelectedCategoryKeys = request.SelectedCategoryKeys.ToArray(),
            State = new Dictionary<string, string>(request.State, StringComparer.OrdinalIgnoreCase),
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
        SavedAnalystWorkflow = workflow;
        AnalystWorkflows = AnalystWorkflows
            .Where(item => !string.Equals(item.Id, workflow.Id, StringComparison.OrdinalIgnoreCase))
            .Append(workflow)
            .ToArray();
        return Task.FromResult(workflow);
    }
    public Task DeleteAnalystWorkflowAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        if (AnalystWorkspaceException is not null)
        {
            return Task.FromException(AnalystWorkspaceException);
        }

        LastDeletedAnalystWorkflowId = workflowId;
        AnalystWorkflows = AnalystWorkflows.Where(item => !string.Equals(item.Id, workflowId, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (SavedAnalystWorkflow is not null && string.Equals(SavedAnalystWorkflow.Id, workflowId, StringComparison.OrdinalIgnoreCase))
        {
            SavedAnalystWorkflow = null;
        }

        return Task.CompletedTask;
    }
    public Task<AnalystNoteDto?> GetAnalystNoteAsync(string targetType, string targetId, CancellationToken cancellationToken = default)
    {
        if (AnalystWorkspaceException is not null)
        {
            return Task.FromException<AnalystNoteDto?>(AnalystWorkspaceException);
        }

        LastRequestedAnalystNoteTargetType = targetType;
        LastRequestedAnalystNoteTargetId = targetId;
        return Task.FromResult(AnalystNote);
    }
    public Task<AnalystNoteDto> SaveAnalystNoteAsync(UpsertAnalystNoteRequest request, CancellationToken cancellationToken = default)
    {
        if (AnalystWorkspaceException is not null)
        {
            return Task.FromException<AnalystNoteDto>(AnalystWorkspaceException);
        }

        LastSavedAnalystNoteRequest = request;
        AnalystNote = new AnalystNoteDto
        {
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            Title = request.Title,
            Content = request.Content,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
        return Task.FromResult(AnalystNote);
    }
    public Task DeleteAnalystNoteAsync(string targetType, string targetId, CancellationToken cancellationToken = default)
    {
        if (AnalystWorkspaceException is not null)
        {
            return Task.FromException(AnalystWorkspaceException);
        }

        LastDeletedAnalystNoteTargetType = targetType;
        LastDeletedAnalystNoteTargetId = targetId;
        AnalystNote = null;
        return Task.CompletedTask;
    }
    public Task<IReadOnlyList<CategoryFamilyDto>> GetCategoryFamiliesAsync(CancellationToken cancellationToken = default) => Task.FromResult(CategoryFamilies);
    public Task<IReadOnlyList<CategoryMetadataDto>> GetEnabledCategoriesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(EnabledCategories.Count == 0
            ? Categories.Where(category => category.IsEnabled).ToArray()
            : EnabledCategories);
    public Task<CategoryDetailDto?> GetCategoryDetailAsync(string categoryKey, CancellationToken cancellationToken = default)
        => CategoryDetailException is null ? Task.FromResult(CategoryDetail) : Task.FromException<CategoryDetailDto?>(CategoryDetailException);

    public Task<IReadOnlyList<SourceDto>> GetSourcesAsync(CancellationToken cancellationToken = default)
        => SourcesException is null ? Task.FromResult(Sources) : Task.FromException<IReadOnlyList<SourceDto>>(SourcesException);
    public Task<SourceDto?> GetSourceAsync(string sourceId, CancellationToken cancellationToken = default)
        => Task.FromResult(Source ?? Sources.FirstOrDefault(item => string.Equals(item.SourceId, sourceId, StringComparison.OrdinalIgnoreCase)));
    public Task<SourceDto> RegisterSourceAsync(RegisterSourceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<SourceDto> UpdateSourceAsync(string sourceId, UpdateSourceRequest request, CancellationToken cancellationToken = default)
    {
        LastUpdatedSourceId = sourceId;
        LastUpdatedSourceRequest = request;
        var source = RequireSource(sourceId);
        var updated = Clone(source, request.DisplayName, request.BaseUrl, new Uri(request.BaseUrl).Host, request.Description, source.IsEnabled, source.SupportedCategoryKeys, source.ThrottlingPolicy, DateTime.UtcNow);
        UpsertSource(updated);
        return Task.FromResult(updated);
    }

    public Task<SourceDto> EnableSourceAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        LastEnabledSourceId = sourceId;
        var source = RequireSource(sourceId);
        var updated = Clone(source, source.DisplayName, source.BaseUrl, source.Host, source.Description, true, source.SupportedCategoryKeys, source.ThrottlingPolicy, DateTime.UtcNow);
        UpsertSource(updated);
        return Task.FromResult(updated);
    }

    public Task<SourceDto> DisableSourceAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        LastDisabledSourceId = sourceId;
        var source = RequireSource(sourceId);
        var updated = Clone(source, source.DisplayName, source.BaseUrl, source.Host, source.Description, false, source.SupportedCategoryKeys, source.ThrottlingPolicy, DateTime.UtcNow);
        UpsertSource(updated);
        return Task.FromResult(updated);
    }

    public Task<SourceDto> AssignCategoriesAsync(string sourceId, AssignSourceCategoriesRequest request, CancellationToken cancellationToken = default)
    {
        LastAssignedCategoriesSourceId = sourceId;
        LastAssignedCategoriesRequest = request;
        var source = RequireSource(sourceId);
        var categoryKeys = request.CategoryKeys
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var updated = Clone(source, source.DisplayName, source.BaseUrl, source.Host, source.Description, source.IsEnabled, categoryKeys, source.ThrottlingPolicy, DateTime.UtcNow);
        UpsertSource(updated);
        return Task.FromResult(updated);
    }

    public Task<SourceDto> UpdateThrottlingAsync(string sourceId, UpdateSourceThrottlingRequest request, CancellationToken cancellationToken = default)
    {
        LastUpdatedThrottlingSourceId = sourceId;
        LastUpdatedThrottlingRequest = request;
        var source = RequireSource(sourceId);
        var throttling = new SourceThrottlingPolicyDto
        {
            MinDelayMs = request.MinDelayMs,
            MaxDelayMs = request.MaxDelayMs,
            MaxConcurrentRequests = request.MaxConcurrentRequests,
            RequestsPerMinute = request.RequestsPerMinute,
            RespectRobotsTxt = request.RespectRobotsTxt
        };
        var updated = Clone(source, source.DisplayName, source.BaseUrl, source.Host, source.Description, source.IsEnabled, source.SupportedCategoryKeys, throttling, DateTime.UtcNow);
        UpsertSource(updated);
        return Task.FromResult(updated);
    }
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

    public Task<IReadOnlyList<SourceQualitySnapshotDto>> GetSourceHistoryAsync(string categoryKey, string? sourceName = null, int? timeRangeDays = null, CancellationToken cancellationToken = default)
    {
        if (AnalyticsException is not null)
        {
            return Task.FromException<IReadOnlyList<SourceQualitySnapshotDto>>(AnalyticsException);
        }

        LastSourceHistoryCategoryKey = categoryKey;
        LastSourceHistorySourceName = sourceName;
        LastSourceHistoryTimeRangeDays = timeRangeDays;
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

    public Task<IReadOnlyList<SourceAttributeDisagreementDto>> GetSourceDisagreementsAsync(string categoryKey, string? sourceName = null, int? timeRangeDays = null, CancellationToken cancellationToken = default)
    {
        if (AnalyticsException is not null)
        {
            return Task.FromException<IReadOnlyList<SourceAttributeDisagreementDto>>(AnalyticsException);
        }

        LastSourceDisagreementsCategoryKey = categoryKey;
        LastSourceDisagreementsSourceName = sourceName;
        LastSourceDisagreementsTimeRangeDays = timeRangeDays;
        return Task.FromResult(SourceDisagreements);
    }

    private SourceDto RequireSource(string sourceId)
    {
        return Source
            ?? Sources.FirstOrDefault(item => string.Equals(item.SourceId, sourceId, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException(sourceId);
    }

    private void UpsertSource(SourceDto source)
    {
        Source = source;
        var items = Sources.ToList();
        var index = items.FindIndex(item => string.Equals(item.SourceId, source.SourceId, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            items[index] = source;
        }
        else
        {
            items.Add(source);
        }

        Sources = items.ToArray();
    }

    private static SourceDto Clone(
        SourceDto source,
        string displayName,
        string baseUrl,
        string host,
        string? description,
        bool isEnabled,
        IReadOnlyList<string> supportedCategoryKeys,
        SourceThrottlingPolicyDto throttlingPolicy,
        DateTime updatedUtc)
    {
        return new SourceDto
        {
            SourceId = source.SourceId,
            DisplayName = displayName,
            BaseUrl = baseUrl,
            Host = host,
            Description = description,
            IsEnabled = isEnabled,
            SupportedCategoryKeys = supportedCategoryKeys.ToArray(),
            ThrottlingPolicy = new SourceThrottlingPolicyDto
            {
                MinDelayMs = throttlingPolicy.MinDelayMs,
                MaxDelayMs = throttlingPolicy.MaxDelayMs,
                MaxConcurrentRequests = throttlingPolicy.MaxConcurrentRequests,
                RequestsPerMinute = throttlingPolicy.RequestsPerMinute,
                RespectRobotsTxt = throttlingPolicy.RespectRobotsTxt
            },
            Readiness = new SourceReadinessDto
            {
                Status = source.Readiness.Status,
                AssignedCategoryCount = source.Readiness.AssignedCategoryCount,
                CrawlableCategoryCount = source.Readiness.CrawlableCategoryCount,
                Summary = source.Readiness.Summary
            },
            Health = new SourceHealthSummaryDto
            {
                Status = source.Health.Status,
                TrustScore = source.Health.TrustScore,
                CoveragePercent = source.Health.CoveragePercent,
                SuccessfulCrawlRate = source.Health.SuccessfulCrawlRate,
                SnapshotUtc = source.Health.SnapshotUtc
            },
            LastActivity = source.LastActivity is null ? null : new SourceLastActivityDto
            {
                TimestampUtc = source.LastActivity.TimestampUtc,
                Status = source.LastActivity.Status,
                DurationMs = source.LastActivity.DurationMs,
                ExtractedProductCount = source.LastActivity.ExtractedProductCount,
                HadMeaningfulChange = source.LastActivity.HadMeaningfulChange,
                MeaningfulChangeSummary = source.LastActivity.MeaningfulChangeSummary,
                ErrorMessage = source.LastActivity.ErrorMessage
            },
            CreatedUtc = source.CreatedUtc,
            UpdatedUtc = updatedUtc
        };
    }
}