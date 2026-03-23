using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ProductNormaliser.AdminApi.Security;

public sealed class ManagementApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<ManagementApiSecurityOptions> securityOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, logger, encoder)
{
    private readonly ManagementApiSecurityOptions securityOptions = securityOptions.Value;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(securityOptions.ApiKeyHeaderName, out var headerValues)
            || string.IsNullOrWhiteSpace(headerValues.ToString()))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var presentedSecret = headerValues.ToString().Trim();
        var key = securityOptions.ApiKeys.FirstOrDefault(candidate => SecretsEqual(candidate.Secret, presentedSecret));
        if (key is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid management API key."));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, key.KeyId),
            new(ClaimTypes.Name, key.DisplayName ?? key.KeyId),
            new(ClaimTypes.Role, string.IsNullOrWhiteSpace(key.Role) ? ManagementSecurityConstants.OperatorRole : key.Role.Trim().ToLowerInvariant()),
            new(ManagementSecurityConstants.ActorTypeClaim, "api_key")
        };

        var identity = new ClaimsIdentity(claims, ManagementSecurityConstants.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ManagementSecurityConstants.AuthenticationScheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = new AuthenticationHeaderValue("ApiKey", $"header=\"{securityOptions.ApiKeyHeaderName}\"").ToString();
        return Task.CompletedTask;
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