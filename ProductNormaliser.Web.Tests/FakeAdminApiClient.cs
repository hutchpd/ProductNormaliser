using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Tests;

internal sealed class FakeAdminApiClient : IProductNormaliserAdminApiClient
{
    public Exception? StatsException { get; set; }
    public Exception? CategoriesException { get; set; }
    public Exception? CategoryDetailException { get; set; }
    public Exception? SourcesException { get; set; }
    public Exception? SourceCandidateDiscoveryException { get; set; }
    public Exception? DiscoveryRunException { get; set; }
    public Exception? SourceRegistrationException { get; set; }
    public Exception? CrawlJobsException { get; set; }
    public StatsDto Stats { get; set; } = new();
    public IReadOnlyList<CategoryMetadataDto> Categories { get; set; } = [];
    public IReadOnlyList<CategoryFamilyDto> CategoryFamilies { get; set; } = [];
    public IReadOnlyList<CategoryMetadataDto> EnabledCategories { get; set; } = [];
    public CategoryDetailDto? CategoryDetail { get; set; }
    public CategorySchemaDto? UpdatedCategorySchema { get; set; }
    public IReadOnlyList<SourceDto> Sources { get; set; } = [];
    public SourceDto? Source { get; set; }
    public SourceOnboardingAutomationSettingsDto AutomationSettings { get; set; } = new()
    {
        DefaultMode = "operator_assisted",
        LlmStatus = "disabled",
        LlmStatusMessage = "LLM validation is disabled for this environment. Set Llm:Enabled=true and configure a local GGUF model to enable it. Discovery uses heuristics only.",
        MaxAutoAcceptedCandidatesPerRun = 1,
        SuggestMinConfidenceScore = 78m,
        AutoAcceptMinConfidenceScore = 90m,
        MinCrawlabilityScore = 60m,
        MinCategoryRelevanceScore = 40m,
        MinExtractabilityScore = 65m,
        MinCatalogLikelihoodScore = 55m,
        MaxDuplicateRiskScore = 15m,
        MinYieldConfidenceScore = 70m
    };
    public SourceCandidateDiscoveryResponseDto SourceCandidateDiscoveryResponse { get; set; } = new();
    public DiscoveryRunDto? DiscoveryRun { get; set; }
    public DiscoveryRunDto? CreatedDiscoveryRun { get; set; }
    public DiscoveryRunPageDto DiscoveryRunPage { get; set; } = new();
    public IReadOnlyList<DiscoveryRunCandidateDto> DiscoveryRunCandidates { get; set; } = [];
    public RegisterSourceRequest? LastRegisteredSourceRequest { get; private set; }
    public DiscoverSourceCandidatesRequest? LastSourceCandidateDiscoveryRequest { get; private set; }
    public CreateDiscoveryRunRequest? LastCreateDiscoveryRunRequest { get; private set; }
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
    public string? LastRequestedDiscoveryRunId { get; private set; }
    public string? LastRequestedDiscoveryRunStatus { get; private set; }
    public int? LastRequestedDiscoveryRunPageNumber { get; private set; }
    public int? LastRequestedDiscoveryRunPageSize { get; private set; }
    public string? LastRequestedDiscoveryRunCandidateStateFilter { get; private set; }
    public string? LastRequestedDiscoveryRunCandidateSort { get; private set; }
    public int? LastRequestedDiscoveryRunCandidatePageNumber { get; private set; }
    public int? LastRequestedDiscoveryRunCandidatePageSize { get; private set; }
    public string? LastPausedDiscoveryRunId { get; private set; }
    public string? LastResumedDiscoveryRunId { get; private set; }
    public string? LastStoppedDiscoveryRunId { get; private set; }
    public string? LastAcceptedDiscoveryRunCandidateRunId { get; private set; }
    public string? LastAcceptedDiscoveryRunCandidateKey { get; private set; }
    public int? LastAcceptedDiscoveryRunCandidateRevision { get; private set; }
    public string? LastDismissedDiscoveryRunCandidateRunId { get; private set; }
    public string? LastDismissedDiscoveryRunCandidateKey { get; private set; }
    public int? LastDismissedDiscoveryRunCandidateRevision { get; private set; }
    public string? LastRestoredDiscoveryRunCandidateRunId { get; private set; }
    public string? LastRestoredDiscoveryRunCandidateKey { get; private set; }
    public int? LastRestoredDiscoveryRunCandidateRevision { get; private set; }
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
    public string? LastUpdatedCategorySchemaCategoryKey { get; private set; }
    public UpdateCategorySchemaRequest? LastUpdatedCategorySchemaRequest { get; private set; }
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
    public Task<CategorySchemaDto> UpdateCategorySchemaAsync(string categoryKey, UpdateCategorySchemaRequest request, CancellationToken cancellationToken = default)
    {
        LastUpdatedCategorySchemaCategoryKey = categoryKey;
        LastUpdatedCategorySchemaRequest = request;

        var schema = UpdatedCategorySchema ?? new CategorySchemaDto
        {
            CategoryKey = categoryKey,
            DisplayName = CategoryDetail?.Schema.DisplayName ?? categoryKey,
            Attributes = request.Attributes.ToArray()
        };

        if (CategoryDetail is not null && string.Equals(CategoryDetail.Metadata.CategoryKey, categoryKey, StringComparison.OrdinalIgnoreCase))
        {
            CategoryDetail = new CategoryDetailDto
            {
                Metadata = CategoryDetail.Metadata,
                Schema = schema
            };
        }

        UpdatedCategorySchema = schema;
        return Task.FromResult(schema);
    }

    public Task<IReadOnlyList<SourceDto>> GetSourcesAsync(CancellationToken cancellationToken = default)
        => SourcesException is null ? Task.FromResult(Sources) : Task.FromException<IReadOnlyList<SourceDto>>(SourcesException);
    public Task<SourceOnboardingAutomationSettingsDto> GetSourceOnboardingAutomationSettingsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(AutomationSettings);
    public Task<SourceDto?> GetSourceAsync(string sourceId, CancellationToken cancellationToken = default)
        => Task.FromResult(Source ?? Sources.FirstOrDefault(item => string.Equals(item.SourceId, sourceId, StringComparison.OrdinalIgnoreCase)));
    public Task<SourceDto> RegisterSourceAsync(RegisterSourceRequest request, CancellationToken cancellationToken = default)
    {
        if (SourceRegistrationException is not null)
        {
            return Task.FromException<SourceDto>(SourceRegistrationException);
        }

        LastRegisteredSourceRequest = request;

        var source = new SourceDto
        {
            SourceId = request.SourceId,
            DisplayName = request.DisplayName,
            BaseUrl = request.BaseUrl,
            Host = new Uri(request.BaseUrl).Host,
            Description = request.Description,
            IsEnabled = request.IsEnabled,
            AllowedMarkets = request.AllowedMarkets.ToArray(),
            PreferredLocale = request.PreferredLocale ?? "en-GB",
            AutomationPolicy = request.AutomationPolicy ?? new SourceAutomationPolicyDto { Mode = "operator_assisted" },
            SupportedCategoryKeys = request.SupportedCategoryKeys.ToArray(),
            DiscoveryProfile = request.DiscoveryProfile ?? new SourceDiscoveryProfileDto
            {
                AllowedMarkets = request.AllowedMarkets.ToArray(),
                PreferredLocale = request.PreferredLocale ?? "en-GB"
            },
            ThrottlingPolicy = request.ThrottlingPolicy ?? new SourceThrottlingPolicyDto
            {
                MinDelayMs = 1000,
                MaxDelayMs = 3000,
                MaxConcurrentRequests = 1,
                RequestsPerMinute = 30,
                RespectRobotsTxt = true
            },
            Readiness = new SourceReadinessDto
            {
                Status = request.SupportedCategoryKeys.Count == 0 ? "Unassigned" : "Ready",
                AssignedCategoryCount = request.SupportedCategoryKeys.Count,
                CrawlableCategoryCount = request.SupportedCategoryKeys.Count,
                Summary = request.SupportedCategoryKeys.Count == 0
                    ? "No categories are currently assigned."
                    : $"All {request.SupportedCategoryKeys.Count} assigned categories are crawl-ready."
            },
            Health = new SourceHealthSummaryDto
            {
                Status = "Unknown",
                Automation = new SourceAutomationPostureDto
                {
                    Status = "advisory",
                    EffectiveMode = "operator_assisted",
                    RecommendedAction = "none"
                }
            },
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        UpsertSource(source);
        return Task.FromResult(source);
    }
    public Task<SourceDto> UpdateSourceAsync(string sourceId, UpdateSourceRequest request, CancellationToken cancellationToken = default)
    {
        LastUpdatedSourceId = sourceId;
        LastUpdatedSourceRequest = request;
        var source = RequireSource(sourceId);
        var updated = Clone(source, request.DisplayName, request.BaseUrl, new Uri(request.BaseUrl).Host, request.Description, source.IsEnabled, request.AllowedMarkets.Count == 0 ? source.AllowedMarkets : request.AllowedMarkets, request.PreferredLocale ?? source.PreferredLocale, request.AutomationPolicy ?? source.AutomationPolicy, source.SupportedCategoryKeys, request.DiscoveryProfile ?? source.DiscoveryProfile, source.ThrottlingPolicy, DateTime.UtcNow);
        UpsertSource(updated);
        return Task.FromResult(updated);
    }

    public Task<SourceDto> EnableSourceAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        LastEnabledSourceId = sourceId;
        var source = RequireSource(sourceId);
        var updated = Clone(source, source.DisplayName, source.BaseUrl, source.Host, source.Description, true, source.AllowedMarkets, source.PreferredLocale, source.AutomationPolicy, source.SupportedCategoryKeys, source.DiscoveryProfile, source.ThrottlingPolicy, DateTime.UtcNow);
        UpsertSource(updated);
        return Task.FromResult(updated);
    }

    public Task<SourceDto> DisableSourceAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        LastDisabledSourceId = sourceId;
        var source = RequireSource(sourceId);
        var updated = Clone(source, source.DisplayName, source.BaseUrl, source.Host, source.Description, false, source.AllowedMarkets, source.PreferredLocale, source.AutomationPolicy, source.SupportedCategoryKeys, source.DiscoveryProfile, source.ThrottlingPolicy, DateTime.UtcNow);
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
        var updated = Clone(source, source.DisplayName, source.BaseUrl, source.Host, source.Description, source.IsEnabled, source.AllowedMarkets, source.PreferredLocale, source.AutomationPolicy, categoryKeys, source.DiscoveryProfile, source.ThrottlingPolicy, DateTime.UtcNow);
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
        var updated = Clone(source, source.DisplayName, source.BaseUrl, source.Host, source.Description, source.IsEnabled, source.AllowedMarkets, source.PreferredLocale, source.AutomationPolicy, source.SupportedCategoryKeys, source.DiscoveryProfile, throttling, DateTime.UtcNow);
        UpsertSource(updated);
        return Task.FromResult(updated);
    }
    public Task<SourceCandidateDiscoveryResponseDto> DiscoverSourceCandidatesAsync(DiscoverSourceCandidatesRequest request, CancellationToken cancellationToken = default)
    {
        LastSourceCandidateDiscoveryRequest = request;
        return SourceCandidateDiscoveryException is null
            ? Task.FromResult(SourceCandidateDiscoveryResponse)
            : Task.FromException<SourceCandidateDiscoveryResponseDto>(SourceCandidateDiscoveryException);
    }
    public Task<DiscoveryRunDto> CreateDiscoveryRunAsync(CreateDiscoveryRunRequest request, CancellationToken cancellationToken = default)
    {
        LastCreateDiscoveryRunRequest = request;
        if (DiscoveryRunException is not null)
        {
            return Task.FromException<DiscoveryRunDto>(DiscoveryRunException);
        }

        var run = CreatedDiscoveryRun ?? DiscoveryRun ?? new DiscoveryRunDto
        {
            RunId = "discovery_run_default",
            RequestedCategoryKeys = request.CategoryKeys.ToArray(),
            Locale = request.Locale,
            Market = request.Market,
            AutomationMode = request.AutomationMode ?? "operator_assisted",
            BrandHints = request.BrandHints.ToArray(),
            MaxCandidates = request.MaxCandidates,
            Status = "queued",
            CurrentStage = "search",
            StatusMessage = "Discovery run is queued and waiting for worker capacity.",
            LlmStatus = "disabled",
            LlmStatusMessage = "LLM validation is disabled.",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        DiscoveryRun = run;
        return Task.FromResult(run);
    }

    public Task<DiscoveryRunDto?> GetDiscoveryRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        LastRequestedDiscoveryRunId = runId;
        if (DiscoveryRunException is not null)
        {
            return Task.FromException<DiscoveryRunDto?>(DiscoveryRunException);
        }

        return Task.FromResult(DiscoveryRun is not null && string.Equals(DiscoveryRun.RunId, runId, StringComparison.OrdinalIgnoreCase)
            ? DiscoveryRun
            : null);
    }

    public Task<DiscoveryRunPageDto> GetDiscoveryRunsAsync(string? status = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        LastRequestedDiscoveryRunStatus = status;
        LastRequestedDiscoveryRunPageNumber = page;
        LastRequestedDiscoveryRunPageSize = pageSize;

        if (DiscoveryRunException is not null)
        {
            return Task.FromException<DiscoveryRunPageDto>(DiscoveryRunException);
        }

        if (DiscoveryRunPage.Items.Count > 0)
        {
            return Task.FromResult(DiscoveryRunPage);
        }

        DiscoveryRunDto[] items = DiscoveryRun is null ? [] : [DiscoveryRun];
        return Task.FromResult(new DiscoveryRunPageDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = items.Length,
            TotalPages = items.Length == 0 ? 0 : 1
        });
    }

    public Task<DiscoveryRunCandidatePageDto> GetDiscoveryRunCandidatesAsync(string runId, DiscoveryRunCandidateQueryDto? query = null, CancellationToken cancellationToken = default)
    {
        LastRequestedDiscoveryRunId = runId;
        LastRequestedDiscoveryRunCandidateStateFilter = query?.StateFilter;
        LastRequestedDiscoveryRunCandidateSort = query?.Sort;
        LastRequestedDiscoveryRunCandidatePageNumber = query?.Page;
        LastRequestedDiscoveryRunCandidatePageSize = query?.PageSize;
        if (DiscoveryRunException is not null)
        {
            return Task.FromException<DiscoveryRunCandidatePageDto>(DiscoveryRunException);
        }

        return Task.FromResult(BuildDiscoveryRunCandidatePage(query));
    }

    public Task<DiscoveryRunDto> PauseDiscoveryRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        LastPausedDiscoveryRunId = runId;
        return MutateRunAsync(runId, "paused", "Discovery run is paused. Resume to continue background execution.");
    }

    public Task<DiscoveryRunDto> ResumeDiscoveryRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        LastResumedDiscoveryRunId = runId;
        return MutateRunAsync(runId, "queued", "Discovery run was resumed and re-queued for worker execution.");
    }

    public Task<DiscoveryRunDto> StopDiscoveryRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        LastStoppedDiscoveryRunId = runId;
        return MutateRunAsync(runId, "cancelled", "Discovery run was cancelled before more background work started.");
    }

    public Task<DiscoveryRunCandidateDto> AcceptDiscoveryRunCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default)
    {
        LastAcceptedDiscoveryRunCandidateRunId = runId;
        LastAcceptedDiscoveryRunCandidateKey = candidateKey;
        LastAcceptedDiscoveryRunCandidateRevision = expectedRevision;
        return MutateCandidateAsync(candidateKey, expectedRevision, "manually_accepted", "Accepted by operator.", acceptedSourceId: candidateKey);
    }

    public Task<DiscoveryRunCandidateDto> DismissDiscoveryRunCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default)
    {
        LastDismissedDiscoveryRunCandidateRunId = runId;
        LastDismissedDiscoveryRunCandidateKey = candidateKey;
        LastDismissedDiscoveryRunCandidateRevision = expectedRevision;
        return MutateCandidateAsync(candidateKey, expectedRevision, "dismissed", "Dismissed by operator.");
    }

    public Task<DiscoveryRunCandidateDto> RestoreDiscoveryRunCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default)
    {
        LastRestoredDiscoveryRunCandidateRunId = runId;
        LastRestoredDiscoveryRunCandidateKey = candidateKey;
        LastRestoredDiscoveryRunCandidateRevision = expectedRevision;
        return MutateCandidateAsync(candidateKey, expectedRevision, "suggested", "Restored to the active candidate queue.");
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

    private Task<DiscoveryRunDto> MutateRunAsync(string runId, string status, string statusMessage)
    {
        if (DiscoveryRunException is not null)
        {
            return Task.FromException<DiscoveryRunDto>(DiscoveryRunException);
        }

        var run = DiscoveryRun is not null && string.Equals(DiscoveryRun.RunId, runId, StringComparison.OrdinalIgnoreCase)
            ? DiscoveryRun
            : throw new KeyNotFoundException(runId);

        DiscoveryRun = new DiscoveryRunDto
        {
            RunId = run.RunId,
            RequestedCategoryKeys = run.RequestedCategoryKeys,
            Locale = run.Locale,
            Market = run.Market,
            AutomationMode = run.AutomationMode,
            BrandHints = run.BrandHints,
            MaxCandidates = run.MaxCandidates,
            Status = status,
            CurrentStage = run.CurrentStage,
            StatusMessage = statusMessage,
            FailureMessage = run.FailureMessage,
            LlmStatus = run.LlmStatus,
            LlmStatusMessage = run.LlmStatusMessage,
            SearchResultCount = run.SearchResultCount,
            CollapsedCandidateCount = run.CollapsedCandidateCount,
            ProbeCompletedCount = run.ProbeCompletedCount,
            LlmQueueDepth = run.LlmQueueDepth,
            LlmCompletedCount = run.LlmCompletedCount,
            LlmTotalElapsedMs = run.LlmTotalElapsedMs,
            LlmAverageElapsedMs = run.LlmAverageElapsedMs,
            SuggestedCandidateCount = run.SuggestedCandidateCount,
            AutoAcceptedCandidateCount = run.AutoAcceptedCandidateCount,
            PublishedCandidateCount = run.PublishedCandidateCount,
            CreatedUtc = run.CreatedUtc,
            UpdatedUtc = DateTime.UtcNow,
            StartedUtc = run.StartedUtc,
            CompletedUtc = status == "cancelled" ? DateTime.UtcNow : run.CompletedUtc,
            CancelRequestedUtc = run.CancelRequestedUtc,
            Diagnostics = run.Diagnostics
        };

        return Task.FromResult(DiscoveryRun);
    }

    private Task<DiscoveryRunCandidateDto> MutateCandidateAsync(string candidateKey, int expectedRevision, string state, string stateMessage, string? acceptedSourceId = null)
    {
        if (DiscoveryRunException is not null)
        {
            return Task.FromException<DiscoveryRunCandidateDto>(DiscoveryRunException);
        }

        var candidate = DiscoveryRunCandidates.FirstOrDefault(item => string.Equals(item.CandidateKey, candidateKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException(candidateKey);

        if (candidate.Revision != expectedRevision)
        {
            return Task.FromException<DiscoveryRunCandidateDto>(new AdminApiValidationException(
                $"Candidate '{candidateKey}' changed while this action was in progress. Refresh the run and retry.",
                new Dictionary<string, string[]>
                {
                    ["request"] = [$"Candidate '{candidateKey}' changed while this action was in progress. Refresh the run and retry."]
                }));
        }

        var updated = new DiscoveryRunCandidateDto
        {
            CandidateKey = candidate.CandidateKey,
            Revision = candidate.Revision + 1,
            State = state,
            PreviousState = candidate.State,
            SupersededByCandidateKey = candidate.SupersededByCandidateKey,
            AcceptedSourceId = acceptedSourceId,
            StateMessage = stateMessage,
            DisplayName = candidate.DisplayName,
            BaseUrl = candidate.BaseUrl,
            Host = candidate.Host,
            CandidateType = candidate.CandidateType,
            AllowedMarkets = candidate.AllowedMarkets,
            PreferredLocale = candidate.PreferredLocale,
            MarketEvidence = candidate.MarketEvidence,
            LocaleEvidence = candidate.LocaleEvidence,
            ConfidenceScore = candidate.ConfidenceScore,
            CrawlabilityScore = candidate.CrawlabilityScore,
            ExtractabilityScore = candidate.ExtractabilityScore,
            DuplicateRiskScore = candidate.DuplicateRiskScore,
            RecommendationStatus = candidate.RecommendationStatus,
            RuntimeExtractionStatus = candidate.RuntimeExtractionStatus,
            RuntimeExtractionMessage = candidate.RuntimeExtractionMessage,
            MatchedCategoryKeys = candidate.MatchedCategoryKeys,
            MatchedBrandHints = candidate.MatchedBrandHints,
            AlreadyRegistered = candidate.AlreadyRegistered,
            DuplicateSourceIds = candidate.DuplicateSourceIds,
            DuplicateSourceDisplayNames = candidate.DuplicateSourceDisplayNames,
            AllowedByGovernance = candidate.AllowedByGovernance,
            GovernanceWarning = candidate.GovernanceWarning,
            Probe = candidate.Probe,
            AutomationAssessment = candidate.AutomationAssessment,
            Reasons = candidate.Reasons
        };

        DiscoveryRunCandidates = DiscoveryRunCandidates
            .Select(item => string.Equals(item.CandidateKey, candidateKey, StringComparison.OrdinalIgnoreCase) ? updated : item)
            .ToArray();
        return Task.FromResult(updated);
    }

    private DiscoveryRunCandidatePageDto BuildDiscoveryRunCandidatePage(DiscoveryRunCandidateQueryDto? query)
    {
        var stateFilter = string.IsNullOrWhiteSpace(query?.StateFilter) ? "all" : query!.StateFilter!.Trim().ToLowerInvariant();
        var sort = string.IsNullOrWhiteSpace(query?.Sort) ? "review_priority" : query!.Sort!.Trim().ToLowerInvariant();
        var pageSize = Math.Clamp(query?.PageSize ?? 12, 1, 100);
        var page = Math.Max(1, query?.Page ?? 1);
        var allItems = DiscoveryRunCandidates.ToArray();
        var filteredItems = stateFilter switch
        {
            "active" => allItems.Where(candidate => !IsArchivedCandidate(candidate.State)),
            "archived" => allItems.Where(candidate => IsArchivedCandidate(candidate.State)),
            "all" => allItems.AsEnumerable(),
            _ => allItems.Where(candidate => string.Equals(candidate.State, stateFilter, StringComparison.OrdinalIgnoreCase))
        };

        var orderedItems = sort switch
        {
            "confidence_desc" => filteredItems
                .OrderByDescending(candidate => candidate.ConfidenceScore)
                .ThenBy(candidate => candidate.DuplicateRiskScore)
                .ThenBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase),
            "duplicate_risk_asc" => filteredItems
                .OrderBy(candidate => candidate.DuplicateRiskScore)
                .ThenByDescending(candidate => candidate.ConfidenceScore)
                .ThenBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase),
            "updated_desc" => filteredItems
                .OrderByDescending(candidate => candidate.ArchivedUtc ?? DateTime.MinValue)
                .ThenBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase),
            _ => filteredItems
                .OrderBy(GetReviewPriorityRank)
                .ThenByDescending(candidate => candidate.ConfidenceScore)
                .ThenBy(candidate => candidate.DuplicateRiskScore)
                .ThenBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
        };

        var totalCount = orderedItems.LongCount();
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        if (totalPages > 0 && page > totalPages)
        {
            page = totalPages;
        }

        return new DiscoveryRunCandidatePageDto
        {
            Items = orderedItems.Skip((page - 1) * pageSize).Take(pageSize).ToArray(),
            StateFilter = stateFilter,
            Sort = sort,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Summary = new DiscoveryRunCandidateRunSummaryDto
            {
                RunCandidateCount = allItems.Length,
                ActiveCandidateCount = allItems.Count(candidate => !IsArchivedCandidate(candidate.State)),
                ArchivedCandidateCount = allItems.Count(candidate => IsArchivedCandidate(candidate.State)),
                ProbeTimeoutCandidateCount = allItems.Count(candidate => candidate.Probe.ProbeTimedOut),
                RepresentativePageFetchFailureCandidateCount = allItems.Count(candidate => candidate.Probe.RepresentativeCategoryPageFetchFailed || candidate.Probe.RepresentativeProductPageFetchFailed),
                RepresentativeCategoryFetchFailureCount = allItems.Count(candidate => candidate.Probe.RepresentativeCategoryPageFetchFailed),
                RepresentativeProductFetchFailureCount = allItems.Count(candidate => candidate.Probe.RepresentativeProductPageFetchFailed),
                LlmTimeoutCandidateCount = allItems.Count(candidate => candidate.Probe.LlmTimedOut)
            }
        };
    }

    private static bool IsArchivedCandidate(string state)
    {
        return string.Equals(state, "dismissed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(state, "archived", StringComparison.OrdinalIgnoreCase)
            || string.Equals(state, "superseded", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetReviewPriorityRank(DiscoveryRunCandidateDto candidate)
    {
        return candidate.State switch
        {
            "suggested" => 0,
            "failed" => 1,
            "manually_accepted" => 2,
            "auto_accepted" => 2,
            "pending" => 3,
            "probing" => 3,
            "awaiting_llm" => 3,
            "dismissed" => 4,
            "archived" => 4,
            "superseded" => 4,
            _ => 5
        };
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
        IReadOnlyList<string> allowedMarkets,
        string preferredLocale,
        SourceAutomationPolicyDto automationPolicy,
        IReadOnlyList<string> supportedCategoryKeys,
        SourceDiscoveryProfileDto discoveryProfile,
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
            AllowedMarkets = allowedMarkets.ToArray(),
            PreferredLocale = preferredLocale,
            AutomationPolicy = automationPolicy,
            SupportedCategoryKeys = supportedCategoryKeys.ToArray(),
            DiscoveryProfile = new SourceDiscoveryProfileDto
            {
                AllowedMarkets = discoveryProfile.AllowedMarkets.ToArray(),
                PreferredLocale = string.IsNullOrWhiteSpace(discoveryProfile.PreferredLocale) ? preferredLocale : discoveryProfile.PreferredLocale,
                CategoryEntryPages = discoveryProfile.CategoryEntryPages.ToDictionary(
                    entry => entry.Key,
                    entry => (IReadOnlyList<string>)entry.Value.ToArray(),
                    StringComparer.OrdinalIgnoreCase),
                SitemapHints = discoveryProfile.SitemapHints.ToArray(),
                AllowedHosts = discoveryProfile.AllowedHosts.ToArray(),
                AllowedPathPrefixes = discoveryProfile.AllowedPathPrefixes.ToArray(),
                ExcludedPathPrefixes = discoveryProfile.ExcludedPathPrefixes.ToArray(),
                ProductUrlPatterns = discoveryProfile.ProductUrlPatterns.ToArray(),
                ListingUrlPatterns = discoveryProfile.ListingUrlPatterns.ToArray(),
                MaxDiscoveryDepth = discoveryProfile.MaxDiscoveryDepth,
                MaxUrlsPerRun = discoveryProfile.MaxUrlsPerRun,
                MaxRetryCount = discoveryProfile.MaxRetryCount,
                RetryBackoffBaseMs = discoveryProfile.RetryBackoffBaseMs,
                RetryBackoffMaxMs = discoveryProfile.RetryBackoffMaxMs
            },
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
                ExtractabilityRate = source.Health.ExtractabilityRate,
                NoProductRate = source.Health.NoProductRate,
                Automation = new SourceAutomationPostureDto
                {
                    Status = source.Health.Automation.Status,
                    EffectiveMode = source.Health.Automation.EffectiveMode,
                    RecommendedAction = source.Health.Automation.RecommendedAction,
                    SnapshotCount = source.Health.Automation.SnapshotCount,
                    DiscoveryBreadthScore = source.Health.Automation.DiscoveryBreadthScore,
                    ProductTargetPromotionRate = source.Health.Automation.ProductTargetPromotionRate,
                    DownstreamYieldScore = source.Health.Automation.DownstreamYieldScore,
                    TrustTrendDelta = source.Health.Automation.TrustTrendDelta,
                    ExtractabilityTrendDelta = source.Health.Automation.ExtractabilityTrendDelta,
                    SupportingReasons = source.Health.Automation.SupportingReasons.ToArray(),
                    BlockingReasons = source.Health.Automation.BlockingReasons.ToArray()
                },
                SnapshotUtc = source.Health.SnapshotUtc
            },
            LastActivity = source.LastActivity is null ? null : new SourceLastActivityDto
            {
                TimestampUtc = source.LastActivity.TimestampUtc,
                Status = source.LastActivity.Status,
                ExtractionOutcome = source.LastActivity.ExtractionOutcome,
                DurationMs = source.LastActivity.DurationMs,
                ExtractedProductCount = source.LastActivity.ExtractedProductCount,
                HadMeaningfulChange = source.LastActivity.HadMeaningfulChange,
                MeaningfulChangeSummary = source.LastActivity.MeaningfulChangeSummary,
                ErrorMessage = source.LastActivity.ErrorMessage
            },
            DiscoveryQueueDepth = source.DiscoveryQueueDepth,
            ListingPagesVisitedLast24Hours = source.ListingPagesVisitedLast24Hours,
            SitemapUrlsProcessedLast24Hours = source.SitemapUrlsProcessedLast24Hours,
            ConfirmedProductUrlsLast24Hours = source.ConfirmedProductUrlsLast24Hours,
            DiscoveryCoverageByCategory = new Dictionary<string, decimal>(source.DiscoveryCoverageByCategory, StringComparer.OrdinalIgnoreCase),
            LastDiscoveryUtc = source.LastDiscoveryUtc,
            SitemapReachable = source.SitemapReachable,
            CreatedUtc = source.CreatedUtc,
            UpdatedUtc = updatedUtc
        };
    }
}