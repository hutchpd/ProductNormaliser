using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Infrastructure.Sources;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.Discovery)]
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
        var provider = new SearchApiSourceCandidateSearchProvider(
          httpClient,
          Options.Create(new SourceCandidateDiscoveryOptions
          {
            SearchTimeoutSeconds = 5,
            MaxSearchQueries = 2,
            SearchApiKey = "test-key"
          }),
          Options.Create(new DiscoveryRunOperationsOptions()));

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
          Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Code), Is.EqualTo(new[] { "search_provider_summary" }));
          Assert.That(result.ProviderResultCount, Is.EqualTo(4));
          Assert.That(result.EligibleResultCount, Is.EqualTo(2));
          Assert.That(result.DiscountedResultCount, Is.EqualTo(2));
          Assert.That(result.MergedDuplicateCount, Is.EqualTo(1));
          Assert.That(result.Candidates, Has.Count.EqualTo(1));
          Assert.That(result.Candidates[0].Host, Is.EqualTo("samsung.com"));
          Assert.That(result.Candidates[0].BaseUrl, Is.EqualTo("https://www.samsung.com/"));
          Assert.That(result.Candidates[0].CandidateType, Is.EqualTo("manufacturer"));
          Assert.That(result.Candidates[0].AllowedMarkets, Is.EqualTo(new[] { "UK" }));
          Assert.That(result.Candidates[0].PreferredLocale, Is.EqualTo("en-GB"));
          Assert.That(result.Candidates[0].MatchedBrandHints, Is.EqualTo(new[] { "Samsung" }));
          Assert.That(result.Candidates[0].MatchedCategoryKeys, Is.EqualTo(new[] { "tv" }));
          Assert.That(result.Diagnostics[0].Message, Does.Contain("Raw results: 4."));
          Assert.That(result.Diagnostics[0].Message, Does.Contain("Eligible mapped hits: 2."));
          Assert.That(result.Diagnostics[0].Message, Does.Contain("Discounted before candidate evaluation: 2."));
        });
    }

      [Test]
      public async Task SearchAsync_ReturnsConfigDiagnostic_WhenApiKeyIsMissing()
      {
        using var httpClient = new HttpClient(new StubHttpMessageHandler((request, cancellationToken) =>
        {
          _ = request;
          _ = cancellationToken;
          return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }))
        {
          BaseAddress = new Uri("https://api.search.brave.com")
        };
        var provider = new SearchApiSourceCandidateSearchProvider(
            httpClient,
            Options.Create(new SourceCandidateDiscoveryOptions
            {
                SearchTimeoutSeconds = 5,
                MaxSearchQueries = 2
            }),
            Options.Create(new DiscoveryRunOperationsOptions()));

        var result = await provider.SearchAsync(new DiscoverSourceCandidatesRequest
        {
          CategoryKeys = ["tv"]
        });

        Assert.Multiple(() =>
        {
          Assert.That(result.Candidates, Is.Empty);
          Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Code), Is.EqualTo(new[] { "search_provider_config_missing" }));
        });
      }

      [Test]
      public async Task SearchAsync_ReturnsRateLimitDiagnostic_WhenProviderReturns429()
      {
        using var httpClient = new HttpClient(new StubHttpMessageHandler((request, cancellationToken) =>
        {
          _ = request;
          _ = cancellationToken;
          return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        }))
        {
          BaseAddress = new Uri("https://api.search.brave.com")
        };
        var provider = new SearchApiSourceCandidateSearchProvider(
            httpClient,
            Options.Create(new SourceCandidateDiscoveryOptions
            {
                SearchTimeoutSeconds = 5,
                MaxSearchQueries = 2,
                SearchApiKey = "test-key"
            }),
            Options.Create(new DiscoveryRunOperationsOptions()));

        var result = await provider.SearchAsync(new DiscoverSourceCandidatesRequest
        {
          CategoryKeys = ["tv"]
        });

        Assert.Multiple(() =>
        {
          Assert.That(result.Candidates, Is.Empty);
          Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Code), Is.EqualTo(new[] { "search_provider_rate_limited", "search_provider_summary" }));
        });
      }

      [Test]
      public async Task SearchAsync_ReturnsFailureDiagnostic_WhenRequestThrows()
      {
        using var httpClient = new HttpClient(new StubHttpMessageHandler((request, cancellationToken) =>
        {
          _ = request;
          _ = cancellationToken;
          throw new HttpRequestException("boom");
        }))
        {
          BaseAddress = new Uri("https://api.search.brave.com")
        };
        var provider = new SearchApiSourceCandidateSearchProvider(
            httpClient,
            Options.Create(new SourceCandidateDiscoveryOptions
            {
                SearchTimeoutSeconds = 5,
                MaxSearchQueries = 2,
                SearchApiKey = "test-key"
            }),
            Options.Create(new DiscoveryRunOperationsOptions()));

        var result = await provider.SearchAsync(new DiscoverSourceCandidatesRequest
        {
          CategoryKeys = ["tv"]
        });

        Assert.Multiple(() =>
        {
          Assert.That(result.Candidates, Is.Empty);
          Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Code), Is.EqualTo(new[] { "search_provider_request_failed", "search_provider_summary" }));
        });
      }

      [Test]
      public async Task SearchAsync_ReturnsTimeoutDiagnostic_WhenSearchBudgetIsExceeded()
      {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
          _ = request;
          await Task.Delay(TimeSpan.FromMilliseconds(1500), cancellationToken);
          return new HttpResponseMessage(HttpStatusCode.OK);
        }))
        {
          BaseAddress = new Uri("https://api.search.brave.com")
        };
        var provider = new SearchApiSourceCandidateSearchProvider(
            httpClient,
            Options.Create(new SourceCandidateDiscoveryOptions
            {
                SearchTimeoutSeconds = 5,
                MaxSearchQueries = 2,
                SearchApiKey = "test-key"
            }),
            Options.Create(new DiscoveryRunOperationsOptions { SearchTimeoutSeconds = 1 }));

        var result = await provider.SearchAsync(new DiscoverSourceCandidatesRequest
        {
          CategoryKeys = ["tv"]
        });

        Assert.Multiple(() =>
        {
          Assert.That(result.Candidates, Is.Empty);
          Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Code), Is.EqualTo(new[] { "search_timeout" }));
        });
      }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }
}