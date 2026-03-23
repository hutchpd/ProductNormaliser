using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Tests;

internal sealed class ProductWebApplicationFactory(FakeAdminApiClient adminApiClient) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IProductNormaliserAdminApiClient>();
            services.AddSingleton<IProductNormaliserAdminApiClient>(adminApiClient);
        });
    }
}
