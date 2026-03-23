namespace ProductNormaliser.AdminApi.Security;

public static class ManagementSecurityConstants
{
    public const string AuthenticationScheme = "ManagementApiKey";
    public const string OperatorPolicy = "ManagementOperator";
    public const string OperatorRole = "operator";
    public const string ViewerRole = "viewer";
    public const string ApiKeyHeaderName = "X-Management-Api-Key";
    public const string ForwardedUserIdHeaderName = "X-Management-User-Id";
    public const string ForwardedUserNameHeaderName = "X-Management-User-Name";
    public const string ActorTypeClaim = "management_actor_type";
}