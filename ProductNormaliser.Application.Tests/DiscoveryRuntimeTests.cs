using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Crawling;
using ProductNormaliser.Infrastructure.Discovery;
using ProductNormaliser.Infrastructure.StructuredData;

namespace ProductNormaliser.Tests;

public sealed class DiscoveryRuntimeTests
{
    [Test]
    public async Task SitemapLocator_PrioritisesRobotsBeforeFallbackEndpointsAndHints()
    {
        var source = CreateSource();
        source.DiscoveryProfile.SitemapHints = ["/catalog-sitemap.xml", "https://alpha.example/sitemap.xml"];
        var httpFetcher = new StubHttpFetcher(new Dictionary<string, FetchResult>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://alpha.example/robots.txt"] = new()
            {
                Url = "https://alpha.example/robots.txt",
                IsSuccess = true,
                StatusCode = 200,
                Html = "User-agent: *\nSitemap: https://alpha.example/robots-sitemap.xml\nSitemap: /nested-sitemap.xml"
            }
        });
        var sut = new SitemapLocator(httpFetcher, new DiscoveryLinkPolicy());

        var result = await sut.LocateAsync(source, CancellationToken.None);

        Assert.That(result, Is.EqualTo(new[]
        {
            "https://alpha.example/robots-sitemap.xml",
            "https://alpha.example/nested-sitemap.xml",
            "https://alpha.example/sitemap.xml",
            "https://alpha.example/sitemap_index.xml",
            "https://alpha.example/catalog-sitemap.xml"
        }));
    }

    [Test]
    public void SitemapParser_ExtractsChildSitemaps()
    {
        const string xml = """
            <sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
              <sitemap><loc>https://alpha.example/sitemap-products.xml</loc></sitemap>
              <sitemap><loc>https://alpha.example/sitemap-monitor.xml</loc></sitemap>
            </sitemapindex>
            """;

        var sut = new SitemapParser();

        var result = sut.Parse(xml);

        Assert.That(result.ChildSitemaps, Is.EqualTo(new[]
        {
            "https://alpha.example/sitemap-products.xml",
            "https://alpha.example/sitemap-monitor.xml"
        }));
    }

    [Test]
    public void SitemapParser_ExtractsCandidateUrlsFromUrlSet()
    {
        const string xml = """
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
              <url><loc>https://alpha.example/category/tv</loc></url>
              <url><loc>https://alpha.example/product/tv-1</loc></url>
            </urlset>
            """;

        var sut = new SitemapParser();

        var result = sut.Parse(xml);

        Assert.That(result.CandidateUrls, Is.EqualTo(new[]
        {
            "https://alpha.example/category/tv",
            "https://alpha.example/product/tv-1"
        }));
    }

    [Test]
    public void DiscoveryLinkPolicy_NormalizesUrlsAndAppliesBoundaryRules()
    {
        var source = CreateSource();
        var sut = new DiscoveryLinkPolicy();

        var normalized = sut.NormalizeUrl("https://alpha.example/product/item-1/?utm_source=newsletter&ref=promo&sku=123");

        Assert.Multiple(() =>
        {
            Assert.That(normalized, Is.EqualTo("https://alpha.example/product/item-1?sku=123"));
            Assert.That(sut.IsAllowed(source, "tv", "https://alpha.example/product/item-1?utm_campaign=spring", depth: 1), Is.True);
            Assert.That(sut.IsAllowed(source, "tv", "https://alpha.example/support/faq", depth: 1), Is.False);
            Assert.That(sut.IsAllowed(source, "tv", "https://external.example/product/item-1", depth: 1), Is.False);
            Assert.That(sut.IsAllowed(source, "tv", "https://alpha.example/category/tv", depth: 5), Is.False);
        });
    }

    [Test]
    public void ProductPageClassifier_PrefersJsonLdProductDetection()
    {
        const string html = """
            <html>
              <head>
                <script type="application/ld+json">
                {
                  "@context": "https://schema.org",
                  "@type": "Product",
                  "name": "Alpha OLED 55",
                  "sku": "OLED55"
                }
                </script>
              </head>
            </html>
            """;

        var sut = new ProductPageClassifier(new SchemaOrgJsonLdExtractor(), new DiscoveryLinkPolicy());

        var result = sut.Classify(CreateSource(), "https://alpha.example/whatever/page", html);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsProductPage, Is.True);
            Assert.That(result.StructuredProductCount, Is.EqualTo(1));
            Assert.That(result.Reason, Does.Contain("JSON-LD"));
        });
    }

    [Test]
    public void ProductLinkExtractor_ExtractsProductPaginationCategoryAndRelatedLinks()
    {
        const string html = """
            <html>
              <body>
                <a class="product-card" href="/product/oled-1?utm_source=nav">OLED 1</a>
                <a rel="next" href="/category/tv?page=2">Next</a>
                <a class="category-link" href="/category/speakers">Speakers</a>
                <a href="/guides/oled-buying-guide">Guide</a>
              </body>
            </html>
            """;

        var sut = new ProductLinkExtractor(new DiscoveryLinkPolicy());

        var result = sut.Extract(CreateSource(), "tv", html, "https://alpha.example/category/tv", depth: 1);

        Assert.Multiple(() =>
        {
            Assert.That(result.ProductLinks, Is.EqualTo(new[] { "https://alpha.example/product/oled-1" }));
            Assert.That(result.PaginationLinks, Is.EqualTo(new[] { "https://alpha.example/category/tv?page=2" }));
            Assert.That(result.CategoryLinks, Is.EqualTo(new[] { "https://alpha.example/category/speakers" }));
            Assert.That(result.RelatedLinks, Is.EqualTo(new[] { "https://alpha.example/guides/oled-buying-guide" }));
        });
    }

    [Test]
    public void ListingPageClassifier_IdentifiesListingPagesFromNavigationAndProductLinks()
    {
        const string html = """
            <html>
              <body>
                <div class="product-grid">
                  <a class="product-card" href="/product/oled-1">OLED 1</a>
                  <a class="product-card" href="/product/oled-2">OLED 2</a>
                  <a class="product-card" href="/product/oled-3">OLED 3</a>
                </div>
                <a rel="next" href="/category/tv?page=2">Next</a>
              </body>
            </html>
            """;

        var linkPolicy = new DiscoveryLinkPolicy();
        var sut = new ListingPageClassifier(new ProductLinkExtractor(linkPolicy), linkPolicy);

        var result = sut.Classify(CreateSource(), "tv", "https://alpha.example/category/tv", html, childDepth: 1);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsListingPage, Is.True);
            Assert.That(result.Links.ProductLinks, Has.Count.EqualTo(3));
            Assert.That(result.Links.PaginationLinks, Has.Count.EqualTo(1));
        });
    }

    private static CrawlSource CreateSource()
    {
        return new CrawlSource
        {
            Id = "alpha",
            DisplayName = "Alpha",
            BaseUrl = "https://alpha.example/",
            Host = "alpha.example",
            DiscoveryProfile = new SourceDiscoveryProfile
            {
                AllowedPathPrefixes = ["/product", "/category", "/guides", "/sitemap", "/catalog"],
                ExcludedPathPrefixes = ["/support"],
                ProductUrlPatterns = ["/product/", "/p/"],
                ListingUrlPatterns = ["/category/", "/collections/", "/browse/"],
                CategoryEntryPages = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["tv"] = ["https://alpha.example/category/tv"]
                },
                MaxDiscoveryDepth = 3
            },
            ThrottlingPolicy = new SourceThrottlingPolicy()
        };
    }

    private sealed class StubHttpFetcher(IReadOnlyDictionary<string, FetchResult> responses) : IHttpFetcher
    {
        public Task<FetchResult> FetchAsync(CrawlTarget target, CancellationToken cancellationToken)
        {
            return Task.FromResult(responses.TryGetValue(target.Url, out var response)
                ? response
                : new FetchResult
                {
                    Url = target.Url,
                    IsSuccess = false,
                    StatusCode = 404,
                    FailureReason = "Not found"
                });
        }
    }
}