using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
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

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(problem?.Title, Is.EqualTo("Management API key required"));
        Assert.That(problem?.Detail, Does.Contain(AdminApiWebApplicationFactory.ApiKeyHeaderName));
    }

    [Test]
    public async Task WrongApiKey_ReturnsUnauthorized()
    {
        using var factory = new AdminApiWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add(AdminApiWebApplicationFactory.ApiKeyHeaderName, "pn-admin-incorrect-key");

        var response = await client.GetAsync("/api/sources");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(problem?.Title, Is.EqualTo("Invalid management API key"));
    }

    [Test]
    public async Task ViewerApiKey_ReturnsForbidden()
    {
        using var factory = new AdminApiWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add(AdminApiWebApplicationFactory.ApiKeyHeaderName, AdminApiWebApplicationFactory.ViewerApiKey);

        var response = await client.GetAsync("/api/sources");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(problem?.Title, Is.EqualTo("Management API access denied"));
    }

    [Test]
    public async Task OperatorApiKey_CanAccessManagementEndpoint()
    {
        using var factory = new AdminApiWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add(AdminApiWebApplicationFactory.ApiKeyHeaderName, AdminApiWebApplicationFactory.OperatorApiKey);

        var response = await client.GetAsync("/api/sources");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task DevelopmentLoopbackBypass_RemainsDisabledByDefault()
    {
        using var factory = new AdminApiWebApplicationFactory(treatRequestAsLoopback: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/api/sources");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task DevelopmentLoopbackBypass_AllowsExplicitLocalDevelopmentAccess()
    {
        using var factory = new AdminApiWebApplicationFactory(
            new Dictionary<string, string?>
            {
                ["ManagementApiSecurity:AllowDevelopmentLoopbackBypass"] = bool.TrueString
            },
            treatRequestAsLoopback: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/api/sources");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}