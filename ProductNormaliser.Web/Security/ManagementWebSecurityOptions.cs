namespace ProductNormaliser.Web.Security;

public sealed class ManagementWebSecurityOptions
{
    public const string SectionName = "ManagementWebSecurity";

    public string CookieName { get; init; } = ".ProductNormaliser.Management";

    public IReadOnlyList<ManagementWebUserOptions> Users { get; init; } = [];
}

public sealed class ManagementWebUserOptions
{
    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string Role { get; init; } = ManagementWebSecurityConstants.OperatorRole;

    public string? DisplayName { get; init; }
}