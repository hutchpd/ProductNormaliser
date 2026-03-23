namespace ProductNormaliser.AdminApi.Security;

public sealed class ManagementApiSecurityOptions
{
    public const string SectionName = "ManagementApiSecurity";

    public string ApiKeyHeaderName { get; init; } = ManagementSecurityConstants.ApiKeyHeaderName;

    public IReadOnlyList<ManagementApiKeyOptions> ApiKeys { get; init; } = [];
}

public sealed class ManagementApiKeyOptions
{
    public string KeyId { get; init; } = string.Empty;

    public string Secret { get; init; } = string.Empty;

    public string Role { get; init; } = ManagementSecurityConstants.OperatorRole;

    public string? DisplayName { get; init; }
}