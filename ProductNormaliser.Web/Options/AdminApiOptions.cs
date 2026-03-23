namespace ProductNormaliser.Web.Options;

public sealed class AdminApiOptions
{
    public const string SectionName = "AdminApi";

    public string BaseUrl { get; init; } = "http://localhost:5209/";

    public string ApiKeyHeaderName { get; init; } = Security.ManagementWebSecurityConstants.ApiKeyHeaderName;

    public string ApiKey { get; init; } = string.Empty;
}