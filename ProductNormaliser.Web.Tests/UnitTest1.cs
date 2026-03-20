using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Tests;

public sealed class AdminApiClientTests
{
    [Test]
    public async Task GetSourcesAsync_DeserialisesSourceList()
    {
        var client = CreateClient(HttpStatusCode.OK, new[]
        {
            new SourceDto
            {
                SourceId = "ao_uk",
                DisplayName = "AO UK",
                BaseUrl = "https://ao.com/",
                Host = "ao.com",
                Description = "Appliances",
                IsEnabled = true,
                SupportedCategoryKeys = ["tv", "refrigerator"],
                ThrottlingPolicy = new SourceThrottlingPolicyDto
                {
                    MinDelayMs = 1000,
                    MaxDelayMs = 4000,
                    MaxConcurrentRequests = 2,
                    RequestsPerMinute = 24,
                    RespectRobotsTxt = true
                },
                CreatedUtc = new DateTime(2026, 03, 20, 10, 00, 00, DateTimeKind.Utc),
                UpdatedUtc = new DateTime(2026, 03, 20, 10, 10, 00, DateTimeKind.Utc)
            }
        });

        var sources = await client.GetSourcesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(sources, Has.Count.EqualTo(1));
            Assert.That(sources[0].DisplayName, Is.EqualTo("AO UK"));
            Assert.That(sources[0].SupportedCategoryKeys, Does.Contain("refrigerator"));
        });
    }

    [Test]
    public async Task GetCategoryDetailAsync_ReturnsNullForNotFound()
    {
        var client = CreateClient(HttpStatusCode.NotFound, payload: null);

        var category = await client.GetCategoryDetailAsync("unknown-category");

        Assert.That(category, Is.Null);
    }

    [Test]
    public void RegisterSourceAsync_ThrowsValidationExceptionForProblemResponse()
    {
        var validation = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["supportedCategoryKeys"] = ["Unknown category keys: smartwatch."]
        })
        {
            Status = 400,
            Title = "One or more validation errors occurred."
        };

        var client = CreateClient(HttpStatusCode.BadRequest, validation, "application/problem+json");

        var action = async () => await client.RegisterSourceAsync(new RegisterSourceRequest
        {
            SourceId = "alpha",
            DisplayName = "Alpha",
            BaseUrl = "https://alpha.example/",
            SupportedCategoryKeys = ["smartwatch"]
        });

        Assert.That(action, Throws.TypeOf<AdminApiValidationException>());
    }

    private static ProductNormaliserAdminApiClient CreateClient(HttpStatusCode statusCode, object? payload, string mediaType = "application/json")
    {
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            var response = new HttpResponseMessage(statusCode);
            if (payload is not null)
            {
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                response.Content = new StringContent(json, Encoding.UTF8, mediaType);
            }

            return Task.FromResult(response);
        });

        return new ProductNormaliserAdminApiClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5209/")
        });
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return handler(request, cancellationToken);
        }
    }
}
