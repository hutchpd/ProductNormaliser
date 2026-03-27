using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ProductNormaliser.AdminApi.Security;

public sealed class ManagementApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<ManagementApiSecurityOptions> securityOptions,
    IHostEnvironment hostEnvironment,
    IManagementRequestOriginEvaluator requestOriginEvaluator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, logger, encoder)
{
    private const string AuthenticationFailureReasonItemKey = "ManagementApiSecurity.AuthenticationFailureReason";
    private const string MissingApiKeyFailureReason = "missing_api_key";
    private const string InvalidApiKeyFailureReason = "invalid_api_key";

    private readonly ManagementApiSecurityOptions securityOptions = securityOptions.Value;
    private readonly IHostEnvironment hostEnvironment = hostEnvironment;
    private readonly IManagementRequestOriginEvaluator requestOriginEvaluator = requestOriginEvaluator;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(securityOptions.ApiKeyHeaderName, out var headerValues)
            || string.IsNullOrWhiteSpace(headerValues.ToString()))
        {
            if (securityOptions.AllowDevelopmentLoopbackBypass
                && hostEnvironment.IsDevelopment()
                && requestOriginEvaluator.IsLoopbackRequest(Context))
            {
                Logger.LogWarning("Management API development loopback bypass granted for {Path}.", Request.Path);
                return Task.FromResult(AuthenticateResult.Success(CreateTicket(
                    keyId: "development-loopback-bypass",
                    displayName: "Development loopback bypass",
                    role: ManagementSecurityConstants.OperatorRole,
                    actorType: "local_development_bypass")));
            }

            Context.Items[AuthenticationFailureReasonItemKey] = MissingApiKeyFailureReason;
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var presentedSecret = headerValues.ToString().Trim();
        var key = securityOptions.ApiKeys.FirstOrDefault(candidate => SecretsEqual(candidate.Secret, presentedSecret));
        if (key is null)
        {
            Context.Items[AuthenticationFailureReasonItemKey] = InvalidApiKeyFailureReason;
            return Task.FromResult(AuthenticateResult.Fail("Invalid management API key."));
        }

        return Task.FromResult(AuthenticateResult.Success(CreateTicket(
            key.KeyId,
            key.DisplayName ?? key.KeyId,
            string.IsNullOrWhiteSpace(key.Role) ? ManagementSecurityConstants.OperatorRole : key.Role.Trim().ToLowerInvariant(),
            actorType: "api_key")));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = new AuthenticationHeaderValue("ApiKey", $"header=\"{securityOptions.ApiKeyHeaderName}\"").ToString();

        var failureReason = Context.Items.TryGetValue(AuthenticationFailureReasonItemKey, out var rawFailureReason)
            ? rawFailureReason as string
            : null;

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = failureReason == InvalidApiKeyFailureReason
                ? "Invalid management API key"
                : "Management API key required",
            Detail = failureReason == InvalidApiKeyFailureReason
                ? $"The provided {securityOptions.ApiKeyHeaderName} header value was not recognised."
                : $"Provide a valid {securityOptions.ApiKeyHeaderName} header to access this Admin API endpoint.",
            Type = "https://httpstatuses.com/401"
        };

        await Response.WriteAsJsonAsync(problem, cancellationToken: Context.RequestAborted);
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Management API access denied",
            Detail = "The presented management API key does not grant operator access to this Admin API endpoint.",
            Type = "https://httpstatuses.com/403"
        };

        return Response.WriteAsJsonAsync(problem, cancellationToken: Context.RequestAborted);
    }

    private AuthenticationTicket CreateTicket(string keyId, string displayName, string role, string actorType)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, keyId),
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.Role, role),
            new(ManagementSecurityConstants.ActorTypeClaim, actorType)
        };

        var identity = new ClaimsIdentity(claims, ManagementSecurityConstants.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        return new AuthenticationTicket(principal, ManagementSecurityConstants.AuthenticationScheme);
    }

    private static bool SecretsEqual(string expected, string provided)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected.Trim());
        var providedBytes = Encoding.UTF8.GetBytes(provided.Trim());
        return expectedBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}