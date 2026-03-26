using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProductNormaliser.Application.AI;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.AI;
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
                var service = new HttpSourceCandidateProbeService(fetcher, new SchemaOrgJsonLdExtractor(), new NoOpPageClassificationService(), Options.Create(new SourceCandidateDiscoveryOptions
        {
            ProbeTimeoutSeconds = 5
            }), Options.Create(new LlmOptions()), NullLogger<HttpSourceCandidateProbeService>.Instance);

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
            Assert.That(result.RuntimeExtractionCompatible, Is.True);
            Assert.That(result.RepresentativeRuntimeProductCount, Is.EqualTo(1));
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
    public async Task ProbeAsync_FlagsRepresentativePageAsNotRuntimeCompatible_WhenOnlyTechnicalAttributesArePresent()
    {
        var fetcher = new FakeHttpFetcher(new Dictionary<string, FetchResult>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://candidate.example/"] = new FetchResult
            {
                Url = "https://candidate.example/",
                IsSuccess = true,
                StatusCode = 200,
                Html = "<html><body><a href=\"/tv/\">TV</a><a href=\"/product/oled-123\">OLED</a></body></html>",
                FetchedUtc = DateTime.UtcNow
            },
            ["https://candidate.example/robots.txt"] = new FetchResult
            {
                Url = "https://candidate.example/robots.txt",
                IsSuccess = true,
                StatusCode = 200,
                Html = "User-agent: *",
                FetchedUtc = DateTime.UtcNow
            },
            ["https://candidate.example/tv/"] = new FetchResult
            {
                Url = "https://candidate.example/tv/",
                IsSuccess = true,
                StatusCode = 200,
                Html = "<html><body><a href=\"/product/oled-123\">OLED TV</a></body></html>",
                FetchedUtc = DateTime.UtcNow
            },
            ["https://candidate.example/product/oled-123"] = new FetchResult
            {
                Url = "https://candidate.example/product/oled-123",
                IsSuccess = true,
                StatusCode = 200,
                Html = "<html><body><section>Specifications</section><table><tr><th>Resolution</th><td>3840x2160</td></tr></table></body></html>",
                FetchedUtc = DateTime.UtcNow
            }
        });
        var service = new HttpSourceCandidateProbeService(
            fetcher,
            new SchemaOrgJsonLdExtractor(),
            new NoOpPageClassificationService(),
            Options.Create(new SourceCandidateDiscoveryOptions { ProbeTimeoutSeconds = 5 }),
            Options.Create(new LlmOptions()),
            NullLogger<HttpSourceCandidateProbeService>.Instance);

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
            Assert.That(result.RepresentativeProductPageReachable, Is.True);
            Assert.That(result.RuntimeExtractionCompatible, Is.False);
            Assert.That(result.RepresentativeRuntimeProductCount, Is.EqualTo(0));
            Assert.That(result.StructuredProductEvidenceDetected, Is.False);
            Assert.That(result.TechnicalAttributeEvidenceDetected, Is.True);
            Assert.That(result.ExtractabilityScore, Is.EqualTo(25m));
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
        var service = new HttpSourceCandidateProbeService(fetcher, new SchemaOrgJsonLdExtractor(), new NoOpPageClassificationService(), Options.Create(new SourceCandidateDiscoveryOptions
        {
            ProbeTimeoutSeconds = 5
        }), Options.Create(new LlmOptions()), NullLogger<HttpSourceCandidateProbeService>.Instance);

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

    [Test]
    public async Task ProbeAsync_ReducesExtractability_WhenLlmRejectsRepresentativeProductPage()
    {
        var fetcher = new FakeHttpFetcher(new Dictionary<string, FetchResult>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://candidate.example/"] = new FetchResult
            {
                Url = "https://candidate.example/",
                IsSuccess = true,
                StatusCode = 200,
                Html = "<html><body><a href=\"/tv/\">TV</a><a href=\"/product/oled-123\">OLED</a></body></html>",
                FetchedUtc = DateTime.UtcNow
            },
            ["https://candidate.example/robots.txt"] = new FetchResult
            {
                Url = "https://candidate.example/robots.txt",
                IsSuccess = true,
                StatusCode = 200,
                Html = "User-agent: *",
                FetchedUtc = DateTime.UtcNow
            },
            ["https://candidate.example/tv/"] = new FetchResult
            {
                Url = "https://candidate.example/tv/",
                IsSuccess = true,
                StatusCode = 200,
                Html = "<html><body><a href=\"/product/oled-123\">OLED TV</a></body></html>",
                FetchedUtc = DateTime.UtcNow
            },
            ["https://candidate.example/product/oled-123"] = new FetchResult
            {
                Url = "https://candidate.example/product/oled-123",
                IsSuccess = true,
                StatusCode = 200,
                Html = "<html><body><section>Specifications</section><table><tr><th>Resolution</th><td>3840x2160</td></tr></table></body></html>",
                FetchedUtc = DateTime.UtcNow
            }
        });
        var service = new HttpSourceCandidateProbeService(
            fetcher,
            new SchemaOrgJsonLdExtractor(),
            new FakeRejectingPageClassificationService(),
            Options.Create(new SourceCandidateDiscoveryOptions { ProbeTimeoutSeconds = 5 }),
            Options.Create(new LlmOptions()),
            NullLogger<HttpSourceCandidateProbeService>.Instance);

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
            Assert.That(result.RepresentativeProductPageReachable, Is.True);
            Assert.That(result.RuntimeExtractionCompatible, Is.False);
            Assert.That(result.RepresentativeRuntimeProductCount, Is.EqualTo(0));
            Assert.That(result.LlmRejectedRepresentativeProductPage, Is.True);
            Assert.That(result.LlmDisagreedWithHeuristics, Is.True);
            Assert.That(result.ExtractabilityScore, Is.EqualTo(0m));
        });
    }

    [Test]
    public async Task ProbeAsync_DoesNotCallLlm_WhenLlmIsDisabled()
    {
        var fetcher = CreateRepresentativeProductFetcher();
        var classifier = new CountingPageClassificationService();
        var service = new HttpSourceCandidateProbeService(
            fetcher,
            new SchemaOrgJsonLdExtractor(),
            classifier,
            Options.Create(new SourceCandidateDiscoveryOptions { ProbeTimeoutSeconds = 5 }),
            Options.Create(new LlmOptions { Enabled = false }),
            NullLogger<HttpSourceCandidateProbeService>.Instance);

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
            Assert.That(classifier.CallCount, Is.EqualTo(0));
            Assert.That(result.LlmAcceptedRepresentativeProductPage, Is.False);
            Assert.That(result.LlmRejectedRepresentativeProductPage, Is.False);
            Assert.That(result.ExtractabilityScore, Is.EqualTo(100m));
            Assert.That(result.LlmReason, Is.Null);
        });
    }

    [Test]
    public async Task ProbeAsync_Continues_WhenLlmInferenceFails()
    {
        var fetcher = CreateRepresentativeProductFetcher();
        var service = new HttpSourceCandidateProbeService(
            fetcher,
            new SchemaOrgJsonLdExtractor(),
            new ThrowingPageClassificationService(),
            Options.Create(new SourceCandidateDiscoveryOptions { ProbeTimeoutSeconds = 5 }),
            Options.Create(new LlmOptions { Enabled = true }),
            NullLogger<HttpSourceCandidateProbeService>.Instance);

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
            Assert.That(result.RepresentativeProductPageReachable, Is.True);
            Assert.That(result.StructuredProductEvidenceDetected, Is.True);
            Assert.That(result.TechnicalAttributeEvidenceDetected, Is.True);
            Assert.That(result.LlmAcceptedRepresentativeProductPage, Is.False);
            Assert.That(result.LlmRejectedRepresentativeProductPage, Is.False);
            Assert.That(result.ExtractabilityScore, Is.EqualTo(100m));
            Assert.That(result.LlmReason, Is.EqualTo("LLM unavailable"));
        });
    }

    private static FakeHttpFetcher CreateRepresentativeProductFetcher()
    {
        return new FakeHttpFetcher(new Dictionary<string, FetchResult>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://candidate.example/"] = new FetchResult
            {
                Url = "https://candidate.example/",
                IsSuccess = true,
                StatusCode = 200,
                Html = "<html><body><a href=\"/tv/\">TV</a><a href=\"/product/oled-123\">OLED</a></body></html>",
                FetchedUtc = DateTime.UtcNow
            },
            ["https://candidate.example/robots.txt"] = new FetchResult
            {
                Url = "https://candidate.example/robots.txt",
                IsSuccess = true,
                StatusCode = 200,
                Html = "User-agent: *",
                FetchedUtc = DateTime.UtcNow
            },
            ["https://candidate.example/tv/"] = new FetchResult
            {
                Url = "https://candidate.example/tv/",
                IsSuccess = true,
                StatusCode = 200,
                Html = "<html><body><a href=\"/product/oled-123\">OLED TV</a></body></html>",
                FetchedUtc = DateTime.UtcNow
            },
            ["https://candidate.example/product/oled-123"] = new FetchResult
            {
                Url = "https://candidate.example/product/oled-123",
                IsSuccess = true,
                StatusCode = 200,
                Html = "<html><head><script type=\"application/ld+json\">{\"@context\":\"https://schema.org\",\"@type\":\"Product\",\"name\":\"OLED TV\"}</script></head><body><section>Specifications</section><table><tr><th>Resolution</th><td>3840x2160</td></tr></table></body></html>",
                FetchedUtc = DateTime.UtcNow
            }
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

    private sealed class FakeRejectingPageClassificationService : IPageClassificationService
    {
        public Task<PageClassificationResult> ClassifyAsync(string content, string category, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PageClassificationResult
            {
                IsProductPage = false,
                HasSpecifications = false,
                Confidence = 0.15
            });
        }
    }

    private sealed class CountingPageClassificationService : IPageClassificationService
    {
        public int CallCount { get; private set; }

        public Task<PageClassificationResult> ClassifyAsync(string content, string category, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new PageClassificationResult
            {
                IsProductPage = true,
                HasSpecifications = true,
                Confidence = 0.8d
            });
        }
    }

    private sealed class ThrowingPageClassificationService : IPageClassificationService
    {
        public Task<PageClassificationResult> ClassifyAsync(string content, string category, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Synthetic inference failure.");
        }
    }
}