using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Tests;

internal sealed class ProductWebApplicationFactory(FakeAdminApiClient adminApiClient) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ManagementWebSecurity:Users:0:Username"] = "operator",
                ["ManagementWebSecurity:Users:0:Password"] = "operator-pass",
                ["ManagementWebSecurity:Users:0:Role"] = "operator",
                ["ManagementWebSecurity:Users:0:DisplayName"] = "Operator User",
                ["ManagementWebSecurity:Users:1:Username"] = "viewer",
                ["ManagementWebSecurity:Users:1:Password"] = "viewer-pass",
                ["ManagementWebSecurity:Users:1:Role"] = "viewer",
                ["ManagementWebSecurity:Users:1:DisplayName"] = "Viewer User"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IProductNormaliserAdminApiClient>();
            services.AddSingleton<IProductNormaliserAdminApiClient>(adminApiClient);
        });
    }
}
