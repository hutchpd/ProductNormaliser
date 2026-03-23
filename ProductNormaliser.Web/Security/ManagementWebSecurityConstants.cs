namespace ProductNormaliser.Web.Security;

public static class ManagementWebSecurityConstants
{
    public const string OperatorPolicy = "ManagementOperator";
    public const string OperatorRole = "operator";
    public const string ViewerRole = "viewer";
    public const string LoginPath = "/Login";
    public const string AccessDeniedPath = "/Forbidden";
    public const string ApiKeyHeaderName = "X-Management-Api-Key";
    public const string ForwardedUserIdHeaderName = "X-Management-User-Id";
    public const string ForwardedUserNameHeaderName = "X-Management-User-Name";
}