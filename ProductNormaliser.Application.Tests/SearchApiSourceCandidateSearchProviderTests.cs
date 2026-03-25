using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Infrastructure.Sources;

namespace ProductNormaliser.Tests;

public sealed class SearchApiSourceCandidateSearchProviderTests
{
    [Test]
    public async Task SearchAsync_MapsSupportedResults_AndFiltersUnsupportedUris()
    {
        var requests = new List<Uri>();
        using var httpClient = new HttpClient(new StubHttpMessageHandler((request, cancellationToken) =>
        {
            requests.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "web": {
                        "results": [
                          {
                            "title": "Samsung Official Store",
                            "url": "https://www.samsung.com/uk/tvs/",
                            "description": "Official Samsung TV range in the UK"
                          },
                          {
                            "title": "Ignored local result",
                            "url": "http://localhost/internal",
                            "description": "Should be filtered"
                          }
                        ]
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });
        }))
        {
            BaseAddress = new Uri("https://api.search.brave.com")
        };
        var provider = new SearchApiSourceCandidateSearchProvider(httpClient, Options.Create(new SourceCandidateDiscoveryOptions
        {
            SearchTimeoutSeconds = 5,
            MaxSearchQueries = 2
        }));

        var result = await provider.SearchAsync(new DiscoverSourceCandidatesRequest
        {
            CategoryKeys = ["tv"],
            BrandHints = ["Samsung"],
            Market = "UK"
        });

        Assert.Multiple(() =>
        {
            Assert.That(requests, Has.Count.EqualTo(2));
            Assert.That(requests.All(uri => uri.AbsolutePath == "/res/v1/web/search"), Is.True);
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Host, Is.EqualTo("samsung.com"));
            Assert.That(result[0].BaseUrl, Is.EqualTo("https://www.samsung.com/"));
            Assert.That(result[0].CandidateType, Is.EqualTo("manufacturer"));
          Assert.That(result[0].AllowedMarkets, Is.EqualTo(new[] { "UK" }));
          Assert.That(result[0].PreferredLocale, Is.EqualTo("en-GB"));
            Assert.That(result[0].MatchedBrandHints, Is.EqualTo(new[] { "Samsung" }));
            Assert.That(result[0].MatchedCategoryKeys, Is.EqualTo(new[] { "tv" }));
        });
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }
}