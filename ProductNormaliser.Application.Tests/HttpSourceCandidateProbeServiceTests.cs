using Microsoft.Extensions.Options;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Crawling;
using ProductNormaliser.Infrastructure.Sources;

namespace ProductNormaliser.Tests;

public sealed class HttpSourceCandidateProbeServiceTests
{
    [Test]
    public async Task ProbeAsync_ExtractsSitemapsCategoryHintsAndUrlPatterns()
    {
        var fetcher = new FakeHttpFetcher(new Dictionary<string, FetchResult>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://candidate.example/"] = new FetchResult
            {
                Url = "https://candidate.example/",
                IsSuccess = true,
                StatusCode = 200,
                Html = """
                    <html>
                      <body>
                        <a href="/tv/">TVs</a>
                        <a href="/category/televisions">Shop televisions</a>
                        <a href="/product/oled-123">OLED</a>
                        <a href="/sitemap-products.xml">Sitemap</a>
                      </body>
                    </html>
                    """,
                FetchedUtc = DateTime.UtcNow
            },
            ["https://candidate.example/robots.txt"] = new FetchResult
            {
                Url = "https://candidate.example/robots.txt",
                IsSuccess = true,
                StatusCode = 200,
                Html = "User-agent: *\nSitemap: https://candidate.example/sitemap.xml",
                FetchedUtc = DateTime.UtcNow
            }
        });
        var service = new HttpSourceCandidateProbeService(fetcher, Options.Create(new SourceCandidateDiscoveryOptions
        {
            ProbeTimeoutSeconds = 5
        }));

        var result = await service.ProbeAsync(new SourceCandidateSearchResult
        {
            CandidateKey = "candidate_example",
            DisplayName = "Candidate Example",
            BaseUrl = "https://candidate.example/",
            Host = "candidate.example",
            CandidateType = "retailer"
        }, ["tv"]);

        Assert.Multiple(() =>
        {
            Assert.That(result.HomePageReachable, Is.True);
            Assert.That(result.RobotsTxtReachable, Is.True);
            Assert.That(result.SitemapDetected, Is.True);
            Assert.That(result.SitemapUrls, Does.Contain("https://candidate.example/sitemap.xml"));
            Assert.That(result.SitemapUrls, Does.Contain("https://candidate.example/sitemap-products.xml"));
            Assert.That(result.CategoryPageHints, Does.Contain("/tv/"));
            Assert.That(result.LikelyListingUrlPatterns, Does.Contain("/category/"));
            Assert.That(result.LikelyProductUrlPatterns, Does.Contain("/product/"));
            Assert.That(result.CategoryRelevanceScore, Is.GreaterThan(0m));
        });
    }

    private sealed class FakeHttpFetcher(IReadOnlyDictionary<string, FetchResult> resultsByUrl) : IHttpFetcher
    {
        public Task<FetchResult> FetchAsync(CrawlTarget target, CancellationToken cancellationToken)
        {
            return Task.FromResult(resultsByUrl.TryGetValue(target.Url, out var result)
                ? result
                : new FetchResult
                {
                    Url = target.Url,
                    IsSuccess = false,
                    StatusCode = 404,
                    FailureReason = "not found",
                    FetchedUtc = DateTime.UtcNow
                });
        }
    }
}