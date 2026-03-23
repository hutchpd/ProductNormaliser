using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ProductNormaliser.Web.Tests;

public sealed class ManagementAuthenticationTests
{
    [Test]
    public async Task UnauthenticatedRequest_RedirectsToLogin()
    {
        using var factory = new ProductWebApplicationFactory(new FakeAdminApiClient());
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.GetAsync("/CrawlJobs/Index");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(response.Headers.Location?.AbsolutePath, Is.EqualTo("/Login"));
        });
    }

    [Test]
    public async Task OperatorUser_CanAccessProtectedPages()
    {
        using var factory = new ProductWebApplicationFactory(new FakeAdminApiClient());
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var loginResponse = await client.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = "operator",
            ["Input.Password"] = "operator-pass",
            ["ReturnUrl"] = "/CrawlJobs/Index"
        }));

        var pageResponse = await client.GetAsync("/CrawlJobs/Index");

        Assert.Multiple(() =>
        {
            Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(pageResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    [Test]
    public async Task ViewerUser_IsRedirectedToForbiddenForOperatorPages()
    {
        using var factory = new ProductWebApplicationFactory(new FakeAdminApiClient());
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        await client.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = "viewer",
            ["Input.Password"] = "viewer-pass",
            ["ReturnUrl"] = "/CrawlJobs/Index"
        }));

        var response = await client.GetAsync("/CrawlJobs/Index");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(response.Headers.Location?.AbsolutePath, Is.EqualTo("/Forbidden"));
        });
    }
}