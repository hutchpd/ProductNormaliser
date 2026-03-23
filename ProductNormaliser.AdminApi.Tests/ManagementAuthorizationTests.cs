using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ProductNormaliser.AdminApi.Tests;

public sealed class ManagementAuthorizationTests
{
    [Test]
    public async Task MissingApiKey_ReturnsUnauthorized()
    {
        using var factory = new AdminApiWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/api/sources");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ViewerApiKey_ReturnsForbidden()
    {
        using var factory = new AdminApiWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("X-Management-Api-Key", "viewer-secret");

        var response = await client.GetAsync("/api/sources");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task OperatorApiKey_CanAccessManagementEndpoint()
    {
        using var factory = new AdminApiWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("X-Management-Api-Key", "operator-secret");

        var response = await client.GetAsync("/api/sources");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}