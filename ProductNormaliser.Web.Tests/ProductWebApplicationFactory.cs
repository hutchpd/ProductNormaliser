using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http;

namespace ProductNormaliser.Web.Tests;

internal sealed class ProductWebApplicationFactory(FakeAdminApiClient adminApiClient) : WebApplicationFactory<Program>
{
    public async Task<HttpClient> CreateOperatorClientAsync()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var loginResponse = await client.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = "operator",
            ["Input.Password"] = "operator-pass",
            ["ReturnUrl"] = "/"
        }));

        if (loginResponse.StatusCode != HttpStatusCode.Redirect)
        {
            client.Dispose();
            throw new InvalidOperationException($"Expected operator login redirect but got {(int)loginResponse.StatusCode}.");
        }

        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ProductWebTestHostConfiguration.Configure(builder, adminApiClient);
    }
}
