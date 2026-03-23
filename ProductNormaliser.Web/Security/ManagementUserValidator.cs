using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace ProductNormaliser.Web.Security;

public interface IManagementUserValidator
{
    ClaimsPrincipal? Validate(string username, string password);
}

public sealed class ManagementUserValidator(IOptions<ManagementWebSecurityOptions> options) : IManagementUserValidator
{
    private readonly ManagementWebSecurityOptions options = options.Value;

    public ClaimsPrincipal? Validate(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var user = options.Users.FirstOrDefault(candidate =>
            string.Equals(candidate.Username, username.Trim(), StringComparison.OrdinalIgnoreCase)
            && SecretsEqual(candidate.Password, password.Trim()));
        if (user is null)
        {
            return null;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Username),
            new(ClaimTypes.Name, user.DisplayName ?? user.Username),
            new(ClaimTypes.Role, string.IsNullOrWhiteSpace(user.Role) ? ManagementWebSecurityConstants.OperatorRole : user.Role.Trim().ToLowerInvariant())
        };

        var identity = new ClaimsIdentity(claims, Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
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