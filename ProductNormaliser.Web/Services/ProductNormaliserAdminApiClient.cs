using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Services;

public sealed class ProductNormaliserAdminApiClient(HttpClient httpClient) : IProductNormaliserAdminApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<StatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        return GetRequiredAsync<StatsDto>("api/stats", AdminApiContractValidator.ValidateStats, cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryMetadataDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await GetRequiredAsync<CategoryMetadataDto[]>("api/categories", AdminApiContractValidator.ValidateCategories, cancellationToken);
    }

    public async Task<IReadOnlyList<AnalystWorkflowDto>> GetAnalystWorkflowsAsync(string? workflowType = null, string? routePath = null, CancellationToken cancellationToken = default)
    {
        var relativeUri = BuildRelativeUri("api/analyst-workspace/workflows", new Dictionary<string, string?>
        {
            ["workflowType"] = workflowType,
            ["routePath"] = routePath
        });

        return await GetRequiredAsync<AnalystWorkflowDto[]>(relativeUri, AdminApiContractValidator.ValidateAnalystWorkflows, cancellationToken);
    }

    public Task<AnalystWorkflowDto?> GetAnalystWorkflowAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        return GetOptionalAsync<AnalystWorkflowDto>($"api/analyst-workspace/workflows/{Uri.EscapeDataString(workflowId)}", AdminApiContractValidator.ValidateAnalystWorkflow, cancellationToken);
    }

    public Task<AnalystWorkflowDto> SaveAnalystWorkflowAsync(UpsertAnalystWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        return SendAsync<AnalystWorkflowDto>(HttpMethod.Post, "api/analyst-workspace/workflows", request, AdminApiContractValidator.ValidateAnalystWorkflow, cancellationToken);
    }

    public Task DeleteAnalystWorkflowAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        return SendNoContentAsync(HttpMethod.Delete, $"api/analyst-workspace/workflows/{Uri.EscapeDataString(workflowId)}", cancellationToken);
    }

    public Task<AnalystNoteDto?> GetAnalystNoteAsync(string targetType, string targetId, CancellationToken cancellationToken = default)
    {
        var relativeUri = BuildRelativeUri("api/analyst-workspace/notes", new Dictionary<string, string?>
        {
            ["targetType"] = targetType,
            ["targetId"] = targetId
        });

        return GetOptionalAsync<AnalystNoteDto>(relativeUri, AdminApiContractValidator.ValidateAnalystNote, cancellationToken);
    }

    public Task<AnalystNoteDto> SaveAnalystNoteAsync(UpsertAnalystNoteRequest request, CancellationToken cancellationToken = default)
    {
        return SendAsync<AnalystNoteDto>(HttpMethod.Post, "api/analyst-workspace/notes", request, AdminApiContractValidator.ValidateAnalystNote, cancellationToken);
    }

    public Task DeleteAnalystNoteAsync(string targetType, string targetId, CancellationToken cancellationToken = default)
    {
        var relativeUri = BuildRelativeUri("api/analyst-workspace/notes", new Dictionary<string, string?>
        {
            ["targetType"] = targetType,
            ["targetId"] = targetId
        });

        return SendNoContentAsync(HttpMethod.Delete, relativeUri, cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryFamilyDto>> GetCategoryFamiliesAsync(CancellationToken cancellationToken = default)
    {
        return await GetRequiredAsync<CategoryFamilyDto[]>("api/categories/families", AdminApiContractValidator.ValidateCategoryFamilies, cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryMetadataDto>> GetEnabledCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await GetRequiredAsync<CategoryMetadataDto[]>("api/categories/enabled", AdminApiContractValidator.ValidateCategories, cancellationToken);
    }

    public Task<CategoryDetailDto?> GetCategoryDetailAsync(string categoryKey, CancellationToken cancellationToken = default)
    {
        return GetOptionalAsync<CategoryDetailDto>($"api/categories/{Uri.EscapeDataString(categoryKey)}/detail", AdminApiContractValidator.ValidateCategoryDetail, cancellationToken);
    }

    public Task<CategorySchemaDto> UpdateCategorySchemaAsync(string categoryKey, UpdateCategorySchemaRequest request, CancellationToken cancellationToken = default)
    {
        return SendAsync<CategorySchemaDto>(HttpMethod.Put, $"api/categories/{Uri.EscapeDataString(categoryKey)}/schema", request, AdminApiContractValidator.ValidateCategorySchema, cancellationToken);
    }

    public async Task<IReadOnlyList<SourceDto>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        return await GetRequiredAsync<SourceDto[]>("api/sources", AdminApiContractValidator.ValidateSources, cancellationToken);
    }

    public Task<SourceOnboardingAutomationSettingsDto> GetSourceOnboardingAutomationSettingsAsync(CancellationToken cancellationToken = default)
    {
        return GetRequiredAsync<SourceOnboardingAutomationSettingsDto>("api/sources/automation-settings", AdminApiContractValidator.ValidateSourceOnboardingAutomationSettings, cancellationToken);
    }

    public Task<SourceDto?> GetSourceAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        return GetOptionalAsync<SourceDto>($"api/sources/{Uri.EscapeDataString(sourceId)}", AdminApiContractValidator.ValidateSource, cancellationToken);
    }

    public Task<SourceDto> RegisterSourceAsync(RegisterSourceRequest request, CancellationToken cancellationToken = default)
    {
        return SendAsync<SourceDto>(HttpMethod.Post, "api/sources", request, AdminApiContractValidator.ValidateSource, cancellationToken);
    }

    public Task<SourceDto> UpdateSourceAsync(string sourceId, UpdateSourceRequest request, CancellationToken cancellationToken = default)
    {
        return SendAsync<SourceDto>(HttpMethod.Put, $"api/sources/{Uri.EscapeDataString(sourceId)}", request, AdminApiContractValidator.ValidateSource, cancellationToken);
    }

    public Task<SourceDto> EnableSourceAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        return SendAsync<SourceDto>(HttpMethod.Post, $"api/sources/{Uri.EscapeDataString(sourceId)}/enable", body: null, AdminApiContractValidator.ValidateSource, cancellationToken);
    }

    public Task<SourceDto> DisableSourceAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        return SendAsync<SourceDto>(HttpMethod.Post, $"api/sources/{Uri.EscapeDataString(sourceId)}/disable", body: null, AdminApiContractValidator.ValidateSource, cancellationToken);
    }

    public Task<SourceDto> AssignCategoriesAsync(string sourceId, AssignSourceCategoriesRequest request, CancellationToken cancellationToken = default)
    {
        return SendAsync<SourceDto>(HttpMethod.Put, $"api/sources/{Uri.EscapeDataString(sourceId)}/categories", request, AdminApiContractValidator.ValidateSource, cancellationToken);
    }

    public Task<SourceDto> UpdateThrottlingAsync(string sourceId, UpdateSourceThrottlingRequest request, CancellationToken cancellationToken = default)
    {
        return SendAsync<SourceDto>(HttpMethod.Put, $"api/sources/{Uri.EscapeDataString(sourceId)}/throttling", request, AdminApiContractValidator.ValidateSource, cancellationToken);
    }

    public Task<SourceCandidateDiscoveryResponseDto> DiscoverSourceCandidatesAsync(DiscoverSourceCandidatesRequest request, CancellationToken cancellationToken = default)
    {
        return SendAsync<SourceCandidateDiscoveryResponseDto>(HttpMethod.Post, "api/sources/candidates/discover", request, AdminApiContractValidator.ValidateSourceCandidateDiscoveryResponse, cancellationToken);
    }

    public Task<DiscoveryRunDto> CreateDiscoveryRunAsync(CreateDiscoveryRunRequest request, CancellationToken cancellationToken = default)
    {
        return SendAsync<DiscoveryRunDto>(HttpMethod.Post, "api/sources/discovery-runs", request, AdminApiContractValidator.ValidateDiscoveryRun, cancellationToken);
    }

    public Task<DiscoveryRunPageDto> GetDiscoveryRunsAsync(string? status = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var relativeUri = BuildRelativeUri("api/sources/discovery-runs", new Dictionary<string, string?>
        {
            ["status"] = status,
            ["page"] = page.ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = pageSize.ToString(CultureInfo.InvariantCulture)
        });

        return GetRequiredAsync<DiscoveryRunPageDto>(relativeUri, AdminApiContractValidator.ValidateDiscoveryRunPage, cancellationToken);
    }

    public Task<DiscoveryRunDto?> GetDiscoveryRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        return GetOptionalAsync<DiscoveryRunDto>($"api/sources/discovery-runs/{Uri.EscapeDataString(runId)}", AdminApiContractValidator.ValidateDiscoveryRun, cancellationToken);
    }

    public async Task<IReadOnlyList<DiscoveryRunCandidateDto>> GetDiscoveryRunCandidatesAsync(string runId, CancellationToken cancellationToken = default)
    {
        return await GetRequiredAsync<DiscoveryRunCandidateDto[]>($"api/sources/discovery-runs/{Uri.EscapeDataString(runId)}/candidates", AdminApiContractValidator.ValidateDiscoveryRunCandidates, cancellationToken);
    }

    public Task<DiscoveryRunDto> PauseDiscoveryRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        return SendAsync<DiscoveryRunDto>(HttpMethod.Post, $"api/sources/discovery-runs/{Uri.EscapeDataString(runId)}/pause", body: null, AdminApiContractValidator.ValidateDiscoveryRun, cancellationToken);
    }

    public Task<DiscoveryRunDto> ResumeDiscoveryRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        return SendAsync<DiscoveryRunDto>(HttpMethod.Post, $"api/sources/discovery-runs/{Uri.EscapeDataString(runId)}/resume", body: null, AdminApiContractValidator.ValidateDiscoveryRun, cancellationToken);
    }

    public Task<DiscoveryRunDto> StopDiscoveryRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        return SendAsync<DiscoveryRunDto>(HttpMethod.Post, $"api/sources/discovery-runs/{Uri.EscapeDataString(runId)}/stop", body: null, AdminApiContractValidator.ValidateDiscoveryRun, cancellationToken);
    }

    public Task<DiscoveryRunCandidateDto> AcceptDiscoveryRunCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default)
    {
        return SendAsync<DiscoveryRunCandidateDto>(HttpMethod.Post, $"api/sources/discovery-runs/{Uri.EscapeDataString(runId)}/candidates/{Uri.EscapeDataString(candidateKey)}/accept", new DiscoveryRunCandidateMutationRequest { ExpectedRevision = expectedRevision }, payload => AdminApiContractValidator.ValidateDiscoveryRunCandidates([payload]), cancellationToken);
    }

    public Task<DiscoveryRunCandidateDto> DismissDiscoveryRunCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default)
    {
        return SendAsync<DiscoveryRunCandidateDto>(HttpMethod.Post, $"api/sources/discovery-runs/{Uri.EscapeDataString(runId)}/candidates/{Uri.EscapeDataString(candidateKey)}/dismiss", new DiscoveryRunCandidateMutationRequest { ExpectedRevision = expectedRevision }, payload => AdminApiContractValidator.ValidateDiscoveryRunCandidates([payload]), cancellationToken);
    }

    public Task<DiscoveryRunCandidateDto> RestoreDiscoveryRunCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default)
    {
        return SendAsync<DiscoveryRunCandidateDto>(HttpMethod.Post, $"api/sources/discovery-runs/{Uri.EscapeDataString(runId)}/candidates/{Uri.EscapeDataString(candidateKey)}/restore", new DiscoveryRunCandidateMutationRequest { ExpectedRevision = expectedRevision }, payload => AdminApiContractValidator.ValidateDiscoveryRunCandidates([payload]), cancellationToken);
    }

    public Task<CrawlJobListResponseDto> GetCrawlJobsAsync(CrawlJobQueryDto? query = null, CancellationToken cancellationToken = default)
    {
        var relativeUri = BuildRelativeUri("api/crawl/jobs", new Dictionary<string, string?>
        {
            ["status"] = query?.Status,
            ["requestType"] = query?.RequestType,
            ["category"] = query?.CategoryKey,
            ["page"] = query?.Page.ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = query?.PageSize.ToString(CultureInfo.InvariantCulture)
        });

        return GetRequiredAsync<CrawlJobListResponseDto>(relativeUri, AdminApiContractValidator.ValidateCrawlJobList, cancellationToken);
    }

    public Task<CrawlJobDto?> GetCrawlJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return GetOptionalAsync<CrawlJobDto>($"api/crawl/jobs/{Uri.EscapeDataString(jobId)}", AdminApiContractValidator.ValidateCrawlJob, cancellationToken);
    }

    public Task<CrawlJobDto> CreateCrawlJobAsync(CreateCrawlJobRequest request, CancellationToken cancellationToken = default)
    {
        return SendAsync<CrawlJobDto>(HttpMethod.Post, "api/crawl/jobs", request, AdminApiContractValidator.ValidateCrawlJob, cancellationToken);
    }

    public Task<CrawlJobDto> CancelCrawlJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return SendAsync<CrawlJobDto>(HttpMethod.Post, $"api/crawl/jobs/{Uri.EscapeDataString(jobId)}/cancel", body: null, AdminApiContractValidator.ValidateCrawlJob, cancellationToken);
    }

    public Task<ProductListResponseDto> GetProductsAsync(ProductListQueryDto? query = null, CancellationToken cancellationToken = default)
    {
        var relativeUri = BuildRelativeUri("api/products", new Dictionary<string, string?>
        {
            ["category"] = query?.CategoryKey,
            ["search"] = query?.Search,
            ["minSourceCount"] = query?.MinSourceCount?.ToString(CultureInfo.InvariantCulture),
            ["freshness"] = query?.Freshness,
            ["conflictStatus"] = query?.ConflictStatus,
            ["completeness"] = query?.CompletenessStatus,
            ["sort"] = query?.Sort,
            ["page"] = query?.Page.ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = query?.PageSize.ToString(CultureInfo.InvariantCulture)
        });

        return GetRequiredAsync<ProductListResponseDto>(relativeUri, AdminApiContractValidator.ValidateProductList, cancellationToken);
    }

    public Task<ProductDetailDto?> GetProductAsync(string productId, CancellationToken cancellationToken = default)
    {
        return GetOptionalAsync<ProductDetailDto>($"api/products/{Uri.EscapeDataString(productId)}", AdminApiContractValidator.ValidateProductDetail, cancellationToken);
    }

    public async Task<IReadOnlyList<ProductChangeEventDto>> GetProductHistoryAsync(string productId, CancellationToken cancellationToken = default)
    {
        return await GetRequiredAsync<ProductChangeEventDto[]>($"api/products/{Uri.EscapeDataString(productId)}/history", AdminApiContractValidator.ValidateProductHistory, cancellationToken);
    }

    public Task<DetailedCoverageResponseDto> GetDetailedCoverageAsync(string categoryKey, CancellationToken cancellationToken = default)
    {
        var relativeUri = BuildRelativeUri("api/quality/coverage/detailed", new Dictionary<string, string?>
        {
            ["categoryKey"] = categoryKey
        });

        return GetRequiredAsync<DetailedCoverageResponseDto>(relativeUri, AdminApiContractValidator.ValidateDetailedCoverage, cancellationToken);
    }

    public async Task<IReadOnlyList<UnmappedAttributeDto>> GetUnmappedAttributesAsync(string categoryKey, CancellationToken cancellationToken = default)
    {
        var relativeUri = BuildRelativeUri("api/quality/unmapped", new Dictionary<string, string?>
        {
            ["categoryKey"] = categoryKey
        });

        return await GetRequiredAsync<UnmappedAttributeDto[]>(relativeUri, AdminApiContractValidator.ValidateUnmappedAttributes, cancellationToken);
    }

    public async Task<IReadOnlyList<SourceQualityScoreDto>> GetSourceQualityScoresAsync(string categoryKey, CancellationToken cancellationToken = default)
    {
        var relativeUri = BuildRelativeUri("api/quality/sources", new Dictionary<string, string?>
        {
            ["categoryKey"] = categoryKey
        });

        return await GetRequiredAsync<SourceQualityScoreDto[]>(relativeUri, AdminApiContractValidator.ValidateSourceQualityScores, cancellationToken);
    }

    public Task<MergeInsightsResponseDto> GetMergeInsightsAsync(string categoryKey, CancellationToken cancellationToken = default)
    {
        var relativeUri = BuildRelativeUri("api/quality/merge-insights", new Dictionary<string, string?>
        {
            ["categoryKey"] = categoryKey
        });

        return GetRequiredAsync<MergeInsightsResponseDto>(relativeUri, AdminApiContractValidator.ValidateMergeInsights, cancellationToken);
    }

    public async Task<IReadOnlyList<SourceQualitySnapshotDto>> GetSourceHistoryAsync(string categoryKey, string? sourceName = null, int? timeRangeDays = null, CancellationToken cancellationToken = default)
    {
        var relativeUri = BuildRelativeUri("api/quality/source-history", new Dictionary<string, string?>
        {
            ["categoryKey"] = categoryKey,
            ["sourceName"] = sourceName,
            ["timeRangeDays"] = timeRangeDays?.ToString(CultureInfo.InvariantCulture)
        });

        return await GetRequiredAsync<SourceQualitySnapshotDto[]>(relativeUri, AdminApiContractValidator.ValidateSourceHistory, cancellationToken);
    }

    public async Task<IReadOnlyList<AttributeStabilityDto>> GetAttributeStabilityAsync(string categoryKey, CancellationToken cancellationToken = default)
    {
        var relativeUri = BuildRelativeUri("api/quality/attribute-stability", new Dictionary<string, string?>
        {
            ["categoryKey"] = categoryKey
        });

        return await GetRequiredAsync<AttributeStabilityDto[]>(relativeUri, AdminApiContractValidator.ValidateAttributeStability, cancellationToken);
    }

    public async Task<IReadOnlyList<SourceAttributeDisagreementDto>> GetSourceDisagreementsAsync(string categoryKey, string? sourceName = null, int? timeRangeDays = null, CancellationToken cancellationToken = default)
    {
        var relativeUri = BuildRelativeUri("api/quality/source-disagreements", new Dictionary<string, string?>
        {
            ["categoryKey"] = categoryKey,
            ["sourceName"] = sourceName,
            ["timeRangeDays"] = timeRangeDays?.ToString(CultureInfo.InvariantCulture)
        });

        return await GetRequiredAsync<SourceAttributeDisagreementDto[]>(relativeUri, AdminApiContractValidator.ValidateSourceDisagreements, cancellationToken);
    }

    private async Task<T?> GetOptionalAsync<T>(string relativeUri, Action<T> validate, CancellationToken cancellationToken)
        where T : class
    {
        using var response = await httpClient.GetAsync(relativeUri, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        var payload = await ReadRequiredAsync<T>(response, cancellationToken);
        validate(payload);
        return payload;
    }

    private async Task<T> GetRequiredAsync<T>(string relativeUri, Action<T> validate, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(relativeUri, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var payload = await ReadRequiredAsync<T>(response, cancellationToken);
        validate(payload);
        return payload;
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string relativeUri, object? body, Action<T> validate, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, relativeUri);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var payload = await ReadRequiredAsync<T>(response, cancellationToken);
        validate(payload);
        return payload;
    }

    private async Task SendNoContentAsync(HttpMethod method, string relativeUri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, relativeUri);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.Conflict)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions, cancellationToken);
            if (problem is not null)
            {
                var flattenedMessage = problem.Errors
                    .SelectMany(entry => entry.Value)
                    .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));
                throw new AdminApiValidationException(
                    flattenedMessage ?? problem.Detail ?? problem.Title ?? "Validation failed.",
                    problem.Errors.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase));
            }
        }

        var detail = response.Content is null
            ? null
            : await response.Content.ReadAsStringAsync(cancellationToken);
        throw new AdminApiException($"Admin API request failed with status {(int)response.StatusCode} ({response.ReasonPhrase}).{(string.IsNullOrWhiteSpace(detail) ? string.Empty : $" {detail}")}");
    }

    private static async Task<T> ReadRequiredAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            throw new AdminApiException("Admin API response body was empty.");
        }

        try
        {
            var payload = JsonSerializer.Deserialize<T>(rawContent, JsonOptions);
            return payload ?? throw new AdminApiException("Admin API response body was empty.");
        }
        catch (JsonException exception)
        {
            throw new AdminApiException($"Admin API response body was invalid JSON. {exception.Message}");
        }
    }

    private static string BuildRelativeUri(string path, IReadOnlyDictionary<string, string?> queryValues)
    {
        var queryString = string.Join("&", queryValues
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
            .Select(entry => $"{Uri.EscapeDataString(entry.Key)}={Uri.EscapeDataString(entry.Value!)}"));

        return string.IsNullOrWhiteSpace(queryString)
            ? path
            : $"{path}?{queryString}";
    }
}

public class AdminApiException(string message) : Exception(message);

public sealed class AdminApiValidationException(string message, IReadOnlyDictionary<string, string[]> errors)
    : AdminApiException(message)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}