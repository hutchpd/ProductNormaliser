using Microsoft.Extensions.Options;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Crawling;
using ProductNormaliser.Infrastructure.Sources;
using ProductNormaliser.Infrastructure.StructuredData;

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
                        },
                        ["https://candidate.example/tv/"] = new FetchResult
                        {
                                Url = "https://candidate.example/tv/",
                                IsSuccess = true,
                                StatusCode = 200,
                                Html = """
                                        <html>
                                            <body>
                                                <a href="/product/oled-123">OLED TV</a>
                                            </body>
                                        </html>
                                        """,
                                FetchedUtc = DateTime.UtcNow
                        },
                        ["https://candidate.example/category/televisions"] = new FetchResult
                        {
                                Url = "https://candidate.example/category/televisions",
                                IsSuccess = true,
                                StatusCode = 200,
                                Html = """
                                        <html>
                                            <body>
                                                <a href="/product/oled-123">OLED TV</a>
                                            </body>
                                        </html>
                                        """,
                                FetchedUtc = DateTime.UtcNow
                        },
                        ["https://candidate.example/product/oled-123"] = new FetchResult
                        {
                                Url = "https://candidate.example/product/oled-123",
                                IsSuccess = true,
                                StatusCode = 200,
                                Html = """
                                        <html>
                                            <head>
                                                <script type="application/ld+json">{"@context":"https://schema.org","@type":"Product","name":"OLED TV"}</script>
                                            </head>
                                            <body>
                                                <section>Specifications</section>
                                                <table><tr><th>Resolution</th><td>3840x2160</td></tr></table>
                                            </body>
                                        </html>
                                        """,
                FetchedUtc = DateTime.UtcNow
            }
        });
                var service = new HttpSourceCandidateProbeService(fetcher, new SchemaOrgJsonLdExtractor(), Options.Create(new SourceCandidateDiscoveryOptions
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
            Assert.That(result.RepresentativeCategoryPageReachable, Is.True);
            Assert.That(result.RepresentativeProductPageReachable, Is.True);
            Assert.That(result.StructuredProductEvidenceDetected, Is.True);
            Assert.That(result.TechnicalAttributeEvidenceDetected, Is.True);
            Assert.That(result.LikelyListingUrlPatterns, Does.Contain("/category/"));
            Assert.That(result.LikelyProductUrlPatterns, Does.Contain("/product/"));
            Assert.That(result.CategoryRelevanceScore, Is.GreaterThan(0m));
            Assert.That(result.ExtractabilityScore, Is.GreaterThanOrEqualTo(90m));
            Assert.That(result.CatalogLikelihoodScore, Is.GreaterThan(50m));
        });
    }

    [Test]
    public async Task ProbeAsync_DowngradesSupportHeavyNonExtractableCandidate()
    {
        var fetcher = new FakeHttpFetcher(new Dictionary<string, FetchResult>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://support-heavy.example/"] = new FetchResult
            {
                Url = "https://support-heavy.example/",
                IsSuccess = true,
                StatusCode = 200,
                Html = """
                    <html>
                      <body>
                        <a href="/support/tv">TV support</a>
                        <a href="/blog/oled-buying-guide">Blog</a>
                        <a href="/help/warranty">Warranty</a>
                      </body>
                    </html>
                    """,
                FetchedUtc = DateTime.UtcNow
            },
            ["https://support-heavy.example/robots.txt"] = new FetchResult
            {
                Url = "https://support-heavy.example/robots.txt",
                IsSuccess = true,
                StatusCode = 200,
                Html = "User-agent: *",
                FetchedUtc = DateTime.UtcNow
            },
            ["https://support-heavy.example/support/tv"] = new FetchResult
            {
                Url = "https://support-heavy.example/support/tv",
                IsSuccess = true,
                StatusCode = 200,
                Html = "<html><body><a href=\"/help/manuals\">Manuals</a></body></html>",
                FetchedUtc = DateTime.UtcNow
            }
        });
        var service = new HttpSourceCandidateProbeService(fetcher, new SchemaOrgJsonLdExtractor(), Options.Create(new SourceCandidateDiscoveryOptions
        {
            ProbeTimeoutSeconds = 5
        }));

        var result = await service.ProbeAsync(new SourceCandidateSearchResult
        {
            CandidateKey = "support_heavy",
            DisplayName = "Support Heavy",
            BaseUrl = "https://support-heavy.example/",
            Host = "support-heavy.example",
            CandidateType = "retailer"
        }, ["tv"]);

        Assert.Multiple(() =>
        {
            Assert.That(result.HomePageReachable, Is.True);
            Assert.That(result.RepresentativeProductPageReachable, Is.False);
            Assert.That(result.StructuredProductEvidenceDetected, Is.False);
            Assert.That(result.TechnicalAttributeEvidenceDetected, Is.False);
            Assert.That(result.NonCatalogContentHeavy, Is.True);
            Assert.That(result.CatalogLikelihoodScore, Is.LessThanOrEqualTo(40m));
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