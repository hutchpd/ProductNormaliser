using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ProductNormaliser.Application.Governance;

namespace ProductNormaliser.AdminApi.Security;

public sealed class HttpContextManagementActorContext(IHttpContextAccessor httpContextAccessor) : IManagementActorContext
{
    public ManagementActor GetCurrentActor()
    {
        var httpContext = httpContextAccessor.HttpContext;
        var user = httpContext?.User;

        var actorId = user?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user?.Identity?.Name
            ?? "anonymous";
        var actorType = user?.FindFirstValue(ManagementSecurityConstants.ActorTypeClaim) ?? "unknown";
        var actorDisplayName = user?.Identity?.Name;
        var forwardedUserId = ReadHeader(httpContext, ManagementSecurityConstants.ForwardedUserIdHeaderName);
        var forwardedUserName = ReadHeader(httpContext, ManagementSecurityConstants.ForwardedUserNameHeaderName);

        return new ManagementActor
        {
            ActorId = actorId,
            ActorType = actorType,
            ActorDisplayName = actorDisplayName,
            ForwardedUserId = forwardedUserId,
            ForwardedUserDisplayName = forwardedUserName
        };
    }

    private static string? ReadHeader(HttpContext? httpContext, string name)
    {
        var value = httpContext?.Request.Headers[name].ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}