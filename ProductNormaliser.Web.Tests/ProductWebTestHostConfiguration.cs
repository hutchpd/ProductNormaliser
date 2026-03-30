using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Tests;

internal static class ProductWebTestHostConfiguration
{
    private static readonly IReadOnlyDictionary<string, string?> SecuritySettings = new Dictionary<string, string?>
    {
        ["ManagementWebSecurity:Users:0:Username"] = "operator",
        ["ManagementWebSecurity:Users:0:Password"] = "operator-pass",
        ["ManagementWebSecurity:Users:0:Role"] = "operator",
        ["ManagementWebSecurity:Users:0:DisplayName"] = "Operator User",
        ["ManagementWebSecurity:Users:1:Username"] = "viewer",
        ["ManagementWebSecurity:Users:1:Password"] = "viewer-pass",
        ["ManagementWebSecurity:Users:1:Role"] = "viewer",
        ["ManagementWebSecurity:Users:1:DisplayName"] = "Viewer User"
    };

    public static void Configure(IWebHostBuilder builder, FakeAdminApiClient adminApiClient)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(SecuritySettings);
        });
        builder.ConfigureServices(services =>
        {
            ConfigureServices(services, adminApiClient);
        });
    }

    public static void Configure(WebApplicationBuilder builder, FakeAdminApiClient adminApiClient)
    {
        builder.Configuration.AddInMemoryCollection(SecuritySettings);
        ConfigureServices(builder.Services, adminApiClient);
    }

    private static void ConfigureServices(IServiceCollection services, FakeAdminApiClient adminApiClient)
    {
        services.RemoveAll<IProductNormaliserAdminApiClient>();
        services.AddSingleton<IProductNormaliserAdminApiClient>(adminApiClient);
    }
}