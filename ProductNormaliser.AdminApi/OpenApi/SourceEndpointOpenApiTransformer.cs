using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ProductNormaliser.AdminApi.OpenApi;

internal static class SourceEndpointOpenApiTransformer
{
    public static Task ApplyAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var relativePath = NormalizePath(context.Description.RelativePath);
        if (!relativePath.StartsWith("/api/sources", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        var method = context.Description.HttpMethod?.ToUpperInvariant();
        switch (method, relativePath)
        {
            case ("GET", "/api/sources"):
                SetResponseExample(operation, StatusCodes.Status200OK, CreateSourceListExample());
                break;
            case ("GET", "/api/sources/{sourceId}"):
                SetResponseExample(operation, StatusCodes.Status200OK, CreateSourceExample(isEnabled: true, displayName: "Currys UK", categories: ["tv", "monitor"]));
                break;
            case ("POST", "/api/sources"):
                SetResponseExample(operation, StatusCodes.Status201Created, CreateSourceExample(isEnabled: true, displayName: "Currys UK", categories: ["tv", "monitor"]));
                SetResponseExample(operation, StatusCodes.Status400BadRequest, CreateValidationExample("supportedCategoryKeys", "Unknown category keys: smartwatch."), "application/problem+json");
                break;
            case ("PUT", "/api/sources/{sourceId}"):
                SetResponseExample(operation, StatusCodes.Status200OK, CreateSourceExample(isEnabled: true, displayName: "Currys United Kingdom", categories: ["tv", "monitor"]));
                SetResponseExample(operation, StatusCodes.Status400BadRequest, CreateValidationExample("baseUrl", "Base URL must be an absolute HTTP or HTTPS URL."), "application/problem+json");
                break;
            case ("POST", "/api/sources/{sourceId}/enable"):
                SetResponseExample(operation, StatusCodes.Status200OK, CreateSourceExample(isEnabled: true, displayName: "AO", categories: ["refrigerator", "tv"]));
                break;
            case ("POST", "/api/sources/{sourceId}/disable"):
                SetResponseExample(operation, StatusCodes.Status200OK, CreateSourceExample(isEnabled: false, displayName: "AO", categories: ["refrigerator", "tv"]));
                break;
            case ("PUT", "/api/sources/{sourceId}/categories"):
                SetResponseExample(operation, StatusCodes.Status200OK, CreateSourceExample(isEnabled: true, displayName: "AO", categories: ["refrigerator", "tv", "laptop"]));
                SetResponseExample(operation, StatusCodes.Status400BadRequest, CreateValidationExample("categoryKeys", "Unknown category keys: smartwatch."), "application/problem+json");
                break;
            case ("PUT", "/api/sources/{sourceId}/throttling"):
                SetResponseExample(operation, StatusCodes.Status200OK, CreateSourceExample(isEnabled: true, displayName: "AO", categories: ["refrigerator", "tv"], minDelayMs: 1500, maxDelayMs: 5000, requestsPerMinute: 24));
                SetResponseExample(operation, StatusCodes.Status400BadRequest, CreateValidationExample("policy", "Maximum delay must be greater than or equal to minimum delay."), "application/problem+json");
                break;
        }

        return Task.CompletedTask;
    }

    private static string NormalizePath(string? relativePath)
    {
        return string.IsNullOrWhiteSpace(relativePath)
            ? string.Empty
            : "/" + relativePath.Trim('/');
    }

    private static void SetResponseExample(OpenApiOperation operation, int statusCode, JsonNode example, string? preferredContentType = null)
    {
        operation.Responses ??= [];

        var statusCodeKey = statusCode.ToString();
        OpenApiResponse response;
        if (!operation.Responses.TryGetValue(statusCodeKey, out var existingResponse) || existingResponse is not OpenApiResponse concreteResponse)
        {
            response = new OpenApiResponse { Description = statusCodeKey };
            operation.Responses[statusCodeKey] = response;
        }
        else
        {
            response = concreteResponse;
        }

        response.Content ??= new Dictionary<string, OpenApiMediaType>(StringComparer.OrdinalIgnoreCase);

        if (response.Content.Count == 0)
        {
            response.Content[preferredContentType ?? "application/json"] = new OpenApiMediaType();
        }

        if (!string.IsNullOrWhiteSpace(preferredContentType) && !response.Content.ContainsKey(preferredContentType))
        {
            response.Content[preferredContentType] = new OpenApiMediaType();
        }

        foreach (var mediaType in response.Content.Values)
        {
            mediaType.Example = example.DeepClone();
        }
    }

    private static JsonArray CreateSourceListExample()
    {
        var example = new JsonArray();
        example.Add(CreateSourceExample(isEnabled: true, displayName: "Currys UK", categories: ["monitor", "tv"]));
        example.Add(CreateSourceExample(isEnabled: false, displayName: "AO", categories: ["refrigerator", "tv"], minDelayMs: 1500, maxDelayMs: 5000, requestsPerMinute: 24));
        return example;
    }

    private static JsonObject CreateSourceExample(bool isEnabled, string displayName, IReadOnlyList<string> categories, int minDelayMs = 1000, int maxDelayMs = 3500, int requestsPerMinute = 30)
    {
        return new JsonObject
        {
            ["sourceId"] = NormalizeSourceId(displayName),
            ["displayName"] = displayName,
            ["baseUrl"] = $"https://{NormalizeHost(displayName)}/",
            ["host"] = NormalizeHost(displayName),
            ["description"] = $"Managed crawl source for {displayName} product listings.",
            ["isEnabled"] = isEnabled,
            ["supportedCategoryKeys"] = CreateCategoryArray(categories),
            ["throttlingPolicy"] = new JsonObject
            {
                ["minDelayMs"] = minDelayMs,
                ["maxDelayMs"] = maxDelayMs,
                ["maxConcurrentRequests"] = 2,
                ["requestsPerMinute"] = requestsPerMinute,
                ["respectRobotsTxt"] = true
            },
            ["createdUtc"] = "2026-03-20T09:30:00Z",
            ["updatedUtc"] = "2026-03-20T10:15:00Z"
        };
    }

    private static JsonObject CreateValidationExample(string key, string message)
    {
        var errors = new JsonObject();
        var messages = new JsonArray();
        messages.Add(message);
        errors[key] = messages;

        return new JsonObject
        {
            ["type"] = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            ["title"] = "One or more validation errors occurred.",
            ["status"] = StatusCodes.Status400BadRequest,
            ["errors"] = errors
        };
    }

    private static JsonArray CreateCategoryArray(IEnumerable<string> categories)
    {
        var values = new JsonArray();
        foreach (var category in categories)
        {
            values.Add(category);
        }

        return values;
    }

    private static string NormalizeHost(string displayName)
    {
        return NormalizeSourceId(displayName).Replace('_', '-') + ".example";
    }

    private static string NormalizeSourceId(string displayName)
    {
        return string.Concat(displayName.ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '_'))
            .Replace("__", "_")
            .Trim('_');
    }
}