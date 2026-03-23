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
        return GetRequiredAsync<StatsDto>("api/stats", cancellationToken)!;
    }

    public async Task<IReadOnlyList<CategoryMetadataDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await GetRequiredAsync<CategoryMetadataDto[]>("api/categories", cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<CategoryFamilyDto>> GetCategoryFamiliesAsync(CancellationToken cancellationToken = default)
    {
        return await GetRequiredAsync<CategoryFamilyDto[]>("api/categories/families", cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<CategoryMetadataDto>> GetEnabledCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await GetRequiredAsync<CategoryMetadataDto[]>("api/categories/enabled", cancellationToken) ?? [];
    }

    public Task<CategoryDetailDto?> GetCategoryDetailAsync(string categoryKey, CancellationToken cancellationToken = default)
    {
        return GetOptionalAsync<CategoryDetailDto>($"api/categories/{Uri.EscapeDataString(categoryKey)}/detail", cancellationToken);
    }

    public async Task<IReadOnlyList<SourceDto>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        return await GetRequiredAsync<SourceDto[]>("api/sources", cancellationToken) ?? [];
    }

    public Task<SourceDto?> GetSourceAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        return GetOptionalAsync<SourceDto>($"api/sources/{Uri.EscapeDataString(sourceId)}", cancellationToken);
    }

    public Task<SourceDto> RegisterSourceAsync(RegisterSourceRequest request, CancellationToken cancellationToken = default)
    {
        return SendAsync<SourceDto>(HttpMethod.Post, "api/sources", request, cancellationToken);
    }

    public Task<SourceDto> UpdateSourceAsync(string sourceId, UpdateSourceRequest request, CancellationToken cancellationToken = default)
    {
        return SendAsync<SourceDto>(HttpMethod.Put, $"api/sources/{Uri.EscapeDataString(sourceId)}", request, cancellationToken);
    }

    public Task<SourceDto> EnableSourceAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        return SendAsync<SourceDto>(HttpMethod.Post, $"api/sources/{Uri.EscapeDataString(sourceId)}/enable", body: null, cancellationToken);
    }

    public Task<SourceDto> DisableSourceAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        return SendAsync<SourceDto>(HttpMethod.Post, $"api/sources/{Uri.EscapeDataString(sourceId)}/disable", body: null, cancellationToken);
    }

    public Task<SourceDto> AssignCategoriesAsync(string sourceId, AssignSourceCategoriesRequest request, CancellationToken cancellationToken = default)
    {
        return SendAsync<SourceDto>(HttpMethod.Put, $"api/sources/{Uri.EscapeDataString(sourceId)}/categories", request, cancellationToken);
    }

    public Task<SourceDto> UpdateThrottlingAsync(string sourceId, UpdateSourceThrottlingRequest request, CancellationToken cancellationToken = default)
    {
        return SendAsync<SourceDto>(HttpMethod.Put, $"api/sources/{Uri.EscapeDataString(sourceId)}/throttling", request, cancellationToken);
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

        return GetRequiredAsync<CrawlJobListResponseDto>(relativeUri, cancellationToken)!;
    }

    public Task<CrawlJobDto?> GetCrawlJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return GetOptionalAsync<CrawlJobDto>($"api/crawl/jobs/{Uri.EscapeDataString(jobId)}", cancellationToken);
    }

    public Task<CrawlJobDto> CreateCrawlJobAsync(CreateCrawlJobRequest request, CancellationToken cancellationToken = default)
    {
        return SendAsync<CrawlJobDto>(HttpMethod.Post, "api/crawl/jobs", request, cancellationToken);
    }

    public Task<CrawlJobDto> CancelCrawlJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return SendAsync<CrawlJobDto>(HttpMethod.Post, $"api/crawl/jobs/{Uri.EscapeDataString(jobId)}/cancel", body: null, cancellationToken);
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

        return GetRequiredAsync<ProductListResponseDto>(relativeUri, cancellationToken)!;
    }

    public Task<ProductDetailDto?> GetProductAsync(string productId, CancellationToken cancellationToken = default)
    {
        return GetOptionalAsync<ProductDetailDto>($"api/products/{Uri.EscapeDataString(productId)}", cancellationToken);
    }

    public async Task<IReadOnlyList<ProductChangeEventDto>> GetProductHistoryAsync(string productId, CancellationToken cancellationToken = default)
    {
        return await GetRequiredAsync<ProductChangeEventDto[]>($"api/products/{Uri.EscapeDataString(productId)}/history", cancellationToken) ?? [];
    }

    private async Task<T?> GetOptionalAsync<T>(string relativeUri, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(relativeUri, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadRequiredAsync<T>(response, cancellationToken);
    }

    private async Task<T?> GetRequiredAsync<T>(string relativeUri, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(relativeUri, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadRequiredAsync<T>(response, cancellationToken);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string relativeUri, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, relativeUri);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadRequiredAsync<T>(response, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions, cancellationToken);
            if (problem is not null)
            {
                throw new AdminApiValidationException(
                    problem.Title ?? "Validation failed.",
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
        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return payload ?? throw new AdminApiException("Admin API response body was empty.");
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