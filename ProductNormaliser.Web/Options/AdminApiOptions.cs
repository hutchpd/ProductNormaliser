namespace ProductNormaliser.Web.Options;

public sealed class AdminApiOptions
{
    public const string SectionName = "AdminApi";

    public string BaseUrl { get; set; } = "http://localhost:5209/";

    public string ApiKeyHeaderName { get; set; } = Security.ManagementWebSecurityConstants.ApiKeyHeaderName;

    public string ApiKey { get; set; } = string.Empty;
}