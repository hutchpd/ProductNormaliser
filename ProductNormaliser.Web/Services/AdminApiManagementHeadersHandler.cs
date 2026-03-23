using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ProductNormaliser.Web.Options;
using ProductNormaliser.Web.Security;

namespace ProductNormaliser.Web.Services;

public sealed class AdminApiManagementHeadersHandler(
    IHttpContextAccessor httpContextAccessor,
    IOptions<AdminApiOptions> options) : DelegatingHandler
{
    private readonly IHttpContextAccessor httpContextAccessor = httpContextAccessor;
    private readonly AdminApiOptions options = options.Value;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            request.Headers.Remove(options.ApiKeyHeaderName);
            request.Headers.TryAddWithoutValidation(options.ApiKeyHeaderName, options.ApiKey);
        }

        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.Identity.Name;
            var displayName = user.Identity.Name;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                request.Headers.Remove(ManagementWebSecurityConstants.ForwardedUserIdHeaderName);
                request.Headers.TryAddWithoutValidation(ManagementWebSecurityConstants.ForwardedUserIdHeaderName, userId);
            }

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                request.Headers.Remove(ManagementWebSecurityConstants.ForwardedUserNameHeaderName);
                request.Headers.TryAddWithoutValidation(ManagementWebSecurityConstants.ForwardedUserNameHeaderName, displayName);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}