using System.Net;

namespace ProductNormaliser.AdminApi.Security;

public interface IManagementRequestOriginEvaluator
{
    bool IsLoopbackRequest(HttpContext httpContext);
}

public sealed class ManagementRequestOriginEvaluator : IManagementRequestOriginEvaluator
{
    public bool IsLoopbackRequest(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return IsLoopback(httpContext.Connection.RemoteIpAddress)
            && (httpContext.Connection.LocalIpAddress is null || IsLoopback(httpContext.Connection.LocalIpAddress));
    }

    private static bool IsLoopback(IPAddress? address)
        => address is not null && IPAddress.IsLoopback(address);
}