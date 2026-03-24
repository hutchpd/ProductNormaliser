using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ProductNormaliser.Web.Options;
using ProductNormaliser.Web.Security;

namespace ProductNormaliser.Web.Services;

public sealed class AdminApiManagementHeadersHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AdminApiOptions _options;

    public AdminApiManagementHeadersHandler(
        IHttpContextAccessor httpContextAccessor,
        IOptions<AdminApiOptions> options)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Remove(_options.ApiKeyHeaderName);
            request.Headers.TryAddWithoutValidation(_options.ApiKeyHeaderName, _options.ApiKey);
        }

        var user = _httpContextAccessor.HttpContext?.User;
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