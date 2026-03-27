using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Application.Crawls;
using ProductNormaliser.Application.Discovery;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Infrastructure.Crawling;
using ProductNormaliser.Infrastructure.Discovery;
using ProductNormaliser.Infrastructure.Mongo.Repositories;
using ProductNormaliser.Infrastructure.StructuredData;
using ProductNormaliser.Worker;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.Discovery)]
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
        var robotsTxtCache = new RobotsTxtCache(httpFetcher);
        var sut = new SitemapLocator(robotsTxtCache, new DiscoveryLinkPolicy());

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
    public async Task RobotsTxtCache_IsReusedByPolicyChecksAndSitemapLookup()
    {
        var source = CreateSource();
        var httpFetcher = new StubHttpFetcher(new Dictionary<string, FetchResult>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://alpha.example/robots.txt"] = new()
            {
                Url = "https://alpha.example/robots.txt",
                IsSuccess = true,
                StatusCode = 200,
                Html = "User-agent: *\nAllow: /\nDisallow: /support\nSitemap: https://alpha.example/robots-sitemap.xml"
            }
        });
        var robotsTxtCache = new RobotsTxtCache(httpFetcher);
        var robotsPolicyService = new RobotsPolicyService(robotsTxtCache, new FakeCrawlSourceStore(source));
        var sitemapLocator = new SitemapLocator(robotsTxtCache, new DiscoveryLinkPolicy());

        var decision = await robotsPolicyService.EvaluateAsync(new CrawlTarget
        {
            Url = "https://alpha.example/product/item-1",
            CategoryKey = "tv",
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceName"] = source.Id
            }
        }, CancellationToken.None);
        var sitemaps = await sitemapLocator.LocateAsync(source, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(decision.IsAllowed, Is.True);
            Assert.That(sitemaps, Contains.Item("https://alpha.example/robots-sitemap.xml"));
            Assert.That(httpFetcher.FetchCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task RobotsPolicyService_AlwaysEnforcesRobotsRules()
    {
        var source = CreateSource();
        source.ThrottlingPolicy.RespectRobotsTxt = false;
        var httpFetcher = new StubHttpFetcher(new Dictionary<string, FetchResult>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://alpha.example/robots.txt"] = new()
            {
                Url = "https://alpha.example/robots.txt",
                IsSuccess = true,
                StatusCode = 200,
                Html = "User-agent: *\nDisallow: /support"
            }
        });
        var robotsPolicyService = new RobotsPolicyService(new RobotsTxtCache(httpFetcher), new FakeCrawlSourceStore(source));

        var decision = await robotsPolicyService.EvaluateAsync(new CrawlTarget
        {
            Url = "https://alpha.example/support/private",
            CategoryKey = "tv",
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceName"] = source.Id
            }
        }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(decision.IsAllowed, Is.False);
            Assert.That(decision.Reason, Does.Contain("Blocked by robots policy"));
            Assert.That(httpFetcher.FetchCount, Is.EqualTo(1));
        });
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
        source.DiscoveryProfile.AllowedHosts = ["media.alpha.example"];
        var sut = new DiscoveryLinkPolicy();

        var normalized = sut.NormalizeUrl("https://alpha.example/product/item-1/?utm_source=newsletter&ref=promo&sku=123");

        Assert.Multiple(() =>
        {
            Assert.That(normalized, Is.EqualTo("https://alpha.example/product/item-1?sku=123"));
            Assert.That(sut.IsAllowed(source, "tv", "https://alpha.example/product/item-1?utm_campaign=spring", depth: 1), Is.True);
            Assert.That(sut.IsAllowed(source, "tv", "https://media.alpha.example/category/tv?page=2", depth: 1), Is.True);
            Assert.That(sut.IsAllowed(source, "tv", "https://alpha.example/support/faq", depth: 1), Is.False);
            Assert.That(sut.IsAllowed(source, "tv", "https://external.example/product/item-1", depth: 1), Is.False);
            Assert.That(sut.IsAllowed(source, "tv", "https://alpha.example/category/tv", depth: 5), Is.False);
        });
    }

    [Test]
    public void DiscoveryLinkPolicy_RejectsStrongMarketAndLocaleContradictions_UnlessExplicitlyAllowed()
    {
        var source = CreateSource();
        var sut = new DiscoveryLinkPolicy();

        Assert.Multiple(() =>
        {
            Assert.That(sut.IsAllowed(source, "tv", "https://alpha.example/en-us/category/tv", depth: 1), Is.False);
            Assert.That(sut.IsAllowed(source, "tv", "https://alpha.example/us/product/oled-1", depth: 1), Is.False);
        });

        source.DiscoveryProfile.AllowedPathPrefixes.Add("/en-us/category");
        source.DiscoveryProfile.AllowedHosts.Add("us.alpha.example");

        Assert.Multiple(() =>
        {
            Assert.That(sut.IsAllowed(source, "tv", "https://alpha.example/en-us/category/tv", depth: 1), Is.True);
            Assert.That(sut.IsAllowed(source, "tv", "https://us.alpha.example/product/oled-1", depth: 1), Is.True);
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
            Assert.That(result.Reason, Does.Contain("Structured product data"));
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

    [Test]
    public async Task RelatedLinkExpansionService_EnqueuesProductBreadcrumbAndListingLinks()
    {
        const string html = """
            <html>
              <body>
                <nav aria-label="breadcrumb">
                  <a href="/category/tv">TV</a>
                  <a href="/category/tv/oled">OLED</a>
                </nav>
                <section class="related-products">
                  <a class="product-card" href="/product/oled-2">OLED 2</a>
                </section>
                <a rel="next" href="/category/tv?page=2">Next</a>
              </body>
            </html>
            """;

        var source = CreateSource();
        var queueService = new RecordingDiscoveryQueueService();
        var linkPolicy = new DiscoveryLinkPolicy();
        var sut = new RelatedLinkExpansionService(
            new FakeCrawlSourceStore(source),
            new ProductLinkExtractor(linkPolicy),
            new ProductPageClassifier(new SchemaOrgJsonLdExtractor(), linkPolicy),
            new ListingPageClassifier(new ProductLinkExtractor(linkPolicy), linkPolicy),
            linkPolicy,
            queueService,
            new TestLogger<RelatedLinkExpansionService>());

        var result = await sut.ExpandAsync(new CrawlTarget
        {
            Url = "https://alpha.example/product/oled-1",
            CategoryKey = "tv",
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceName"] = source.Id
            }
        }, html, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.EnqueuedProductUrls, Is.EqualTo(1));
            Assert.That(result.EnqueuedListingUrls, Is.EqualTo(3));
            Assert.That(queueService.Enqueued.Select(item => (item.ItemType, item.Url)), Is.EqualTo(new[]
            {
                ("product", "https://alpha.example/product/oled-2"),
                ("listing", "https://alpha.example/category/tv?page=2"),
                ("listing", "https://alpha.example/category/tv"),
                ("listing", "https://alpha.example/category/tv/oled")
            }));
        });
    }

    [Test]
    public async Task RelatedLinkExpansionService_RejectsContradictoryMarketAndLocaleLinks()
    {
        const string html = """
            <html>
              <body>
                <nav aria-label="breadcrumb">
                  <a href="/category/tv">TV</a>
                  <a href="/en-us/category/tv">US TV</a>
                </nav>
                <a class="product-card" href="/product/oled-2">OLED 2</a>
                <a class="product-card" href="/us/product/oled-3">US OLED 3</a>
                <a rel="next" href="/category/tv?page=2">Next</a>
                <a rel="next" href="/en-us/category/tv?page=2">US Next</a>
              </body>
            </html>
            """;

        var source = CreateSource();
        var queueService = new RecordingDiscoveryQueueService();
        var linkPolicy = new DiscoveryLinkPolicy();
        var sut = new RelatedLinkExpansionService(
            new FakeCrawlSourceStore(source),
            new ProductLinkExtractor(linkPolicy),
            new ProductPageClassifier(new SchemaOrgJsonLdExtractor(), linkPolicy),
            new ListingPageClassifier(new ProductLinkExtractor(linkPolicy), linkPolicy),
            linkPolicy,
            queueService,
            new TestLogger<RelatedLinkExpansionService>());

        var result = await sut.ExpandAsync(new CrawlTarget
        {
            Url = "https://alpha.example/product/oled-1",
            CategoryKey = "tv",
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceName"] = source.Id
            }
        }, html, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.EnqueuedProductUrls, Is.EqualTo(1));
            Assert.That(result.EnqueuedListingUrls, Is.EqualTo(2));
            Assert.That(queueService.Enqueued.Select(item => (item.ItemType, item.Url)), Is.EqualTo(new[]
            {
                ("product", "https://alpha.example/product/oled-2"),
                ("listing", "https://alpha.example/category/tv?page=2"),
                ("listing", "https://alpha.example/category/tv")
            }));
        });
    }

    [Test]
    public async Task DiscoveryOrchestrator_RequeuedRootSeedCanSurfaceNewProductLinksOverTime()
    {
        var source = CreateSource();
        source.DiscoveryProfile.SeedReseedIntervalHours = 1;

        var queueStore = new FakeDiscoveryQueueStore();
        var discoveredStore = new FakeDiscoveredUrlStore();
        var productQueueStore = new FakeProductTargetQueueStore();
        var queueWriter = new RecordingCrawlJobQueueWriter(productQueueStore);
        var progressService = new DiscoveryJobProgressService(new FakeCrawlJobStore());
        var discoveryQueueService = new DiscoveryQueueService(
            queueStore,
            discoveredStore,
            new FakeCrawlSourceStore(source),
            new DiscoveryLinkPolicy(),
            new ProductTargetEnqueuer(productQueueStore, queueWriter, progressService),
            progressService);
        var fetcher = new MutableHttpFetcher(new Dictionary<string, FetchResult>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://alpha.example/category/tv"] = new()
            {
                Url = "https://alpha.example/category/tv",
                IsSuccess = true,
                StatusCode = 200,
                Html = "<html><body><div class=\"product-grid\"><a class=\"product-card\" href=\"/product/oled-1\">OLED 1</a></div></body></html>"
            }
        });
        var linkPolicy = new DiscoveryLinkPolicy();
        var orchestrator = new DiscoveryOrchestrator(
            new FakeCrawlSourceStore(source),
            new AllowAllRobotsPolicyService(),
            fetcher,
            new SitemapParser(),
            linkPolicy,
            new ProductPageClassifier(new SchemaOrgJsonLdExtractor(), linkPolicy),
            new ListingPageClassifier(new ProductLinkExtractor(linkPolicy), linkPolicy),
            discoveryQueueService,
            new TestLogger<DiscoveryOrchestrator>());

        var firstEnqueue = await discoveryQueueService.EnqueueAsync(source, "tv", "https://alpha.example/category/tv", "listing", 0, null, null, CancellationToken.None);
        var firstLease = await discoveryQueueService.DequeueAsync(CancellationToken.None);
        var firstResult = await orchestrator.ProcessAsync(firstLease!.Item, CancellationToken.None);
        await discoveryQueueService.MarkCompletedAsync(firstLease.QueueItemId, CancellationToken.None);

        queueStore.Items[firstLease.QueueItemId].CompletedUtc = DateTime.UtcNow.AddHours(-2);
        fetcher.SetHtml("https://alpha.example/category/tv", "<html><body><div class=\"product-grid\"><a class=\"product-card\" href=\"/product/oled-1\">OLED 1</a><a class=\"product-card\" href=\"/product/oled-2\">OLED 2</a></div></body></html>");

        var secondEnqueue = await discoveryQueueService.EnqueueAsync(source, "tv", "https://alpha.example/category/tv", "listing", 0, null, null, CancellationToken.None);
        var secondLease = await discoveryQueueService.DequeueAsync(CancellationToken.None);
        var secondResult = await orchestrator.ProcessAsync(secondLease!.Item, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(firstEnqueue, Is.True);
            Assert.That(firstResult.Status, Is.EqualTo("completed"));
            Assert.That(secondEnqueue, Is.True);
            Assert.That(secondResult.Status, Is.EqualTo("completed"));
            Assert.That(queueWriter.Items, Has.Count.EqualTo(2));
            Assert.That(queueWriter.Items.Select(item => item.SourceUrl), Is.EqualTo(new[]
            {
                "https://alpha.example/product/oled-1",
                "https://alpha.example/product/oled-2"
            }));
        });
    }

    [Test]
    public async Task DiscoveryOrchestrator_RequeuedRootSeedStillHonoursMarketLocaleBoundaries()
    {
        var source = CreateSource();
        source.DiscoveryProfile.SeedReseedIntervalHours = 1;

        var queueStore = new FakeDiscoveryQueueStore();
        var discoveredStore = new FakeDiscoveredUrlStore();
        var productQueueStore = new FakeProductTargetQueueStore();
        var queueWriter = new RecordingCrawlJobQueueWriter(productQueueStore);
        var progressService = new DiscoveryJobProgressService(new FakeCrawlJobStore());
        var discoveryQueueService = new DiscoveryQueueService(
            queueStore,
            discoveredStore,
            new FakeCrawlSourceStore(source),
            new DiscoveryLinkPolicy(),
            new ProductTargetEnqueuer(productQueueStore, queueWriter, progressService),
            progressService);
        var fetcher = new MutableHttpFetcher(new Dictionary<string, FetchResult>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://alpha.example/category/tv"] = new()
            {
                Url = "https://alpha.example/category/tv",
                IsSuccess = true,
                StatusCode = 200,
                Html = "<html><body><div class=\"product-grid\"><a class=\"product-card\" href=\"/us/product/oled-1\">US OLED 1</a></div></body></html>"
            }
        });
        var linkPolicy = new DiscoveryLinkPolicy();
        var orchestrator = new DiscoveryOrchestrator(
            new FakeCrawlSourceStore(source),
            new AllowAllRobotsPolicyService(),
            fetcher,
            new SitemapParser(),
            linkPolicy,
            new ProductPageClassifier(new SchemaOrgJsonLdExtractor(), linkPolicy),
            new ListingPageClassifier(new ProductLinkExtractor(linkPolicy), linkPolicy),
            discoveryQueueService,
            new TestLogger<DiscoveryOrchestrator>());

        var firstEnqueue = await discoveryQueueService.EnqueueAsync(source, "tv", "https://alpha.example/category/tv", "listing", 0, null, null, CancellationToken.None);
        var firstLease = await discoveryQueueService.DequeueAsync(CancellationToken.None);
        var firstResult = await orchestrator.ProcessAsync(firstLease!.Item, CancellationToken.None);
        await discoveryQueueService.MarkCompletedAsync(firstLease.QueueItemId, CancellationToken.None);

        queueStore.Items[firstLease.QueueItemId].CompletedUtc = DateTime.UtcNow.AddHours(-2);
        fetcher.SetHtml("https://alpha.example/category/tv", "<html><body><div class=\"product-grid\"><a class=\"product-card\" href=\"/us/product/oled-1\">US OLED 1</a><a class=\"product-card\" href=\"/product/oled-2\">OLED 2</a></div></body></html>");

        var secondEnqueue = await discoveryQueueService.EnqueueAsync(source, "tv", "https://alpha.example/category/tv", "listing", 0, null, null, CancellationToken.None);
        var secondLease = await discoveryQueueService.DequeueAsync(CancellationToken.None);
        var secondResult = await orchestrator.ProcessAsync(secondLease!.Item, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(firstEnqueue, Is.True);
            Assert.That(firstResult.Status, Is.EqualTo("completed"));
            Assert.That(secondEnqueue, Is.True);
            Assert.That(secondResult.Status, Is.EqualTo("completed"));
            Assert.That(queueWriter.Items, Has.Count.EqualTo(1));
            Assert.That(queueWriter.Items.Select(item => item.SourceUrl), Is.EqualTo(new[] { "https://alpha.example/product/oled-2" }));
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
            IsEnabled = true,
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
        public int FetchCount { get; private set; }

        public Task<FetchResult> FetchAsync(CrawlTarget target, CancellationToken cancellationToken)
        {
            FetchCount += 1;
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

    private sealed class MutableHttpFetcher(IReadOnlyDictionary<string, FetchResult> initialResponses) : IHttpFetcher
    {
        private readonly Dictionary<string, FetchResult> responses = new(initialResponses, StringComparer.OrdinalIgnoreCase);

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

        public void SetHtml(string url, string html)
        {
            responses[url] = new FetchResult
            {
                Url = url,
                IsSuccess = true,
                StatusCode = 200,
                Html = html
            };
        }
    }

    private sealed class FakeCrawlSourceStore(params CrawlSource[] sources) : ICrawlSourceStore
    {
        private readonly List<CrawlSource> items = [.. sources];

        public Task<IReadOnlyList<CrawlSource>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<CrawlSource>>(items);
        }

        public Task<CrawlSource?> GetAsync(string sourceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(items.FirstOrDefault(item => string.Equals(item.Id, sourceId, StringComparison.OrdinalIgnoreCase)));
        }

        public Task UpsertAsync(CrawlSource source, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingDiscoveryQueueService : IDiscoveryQueueService
    {
        public List<(string ItemType, string Url)> Enqueued { get; } = [];

        public Task<DiscoveryQueueLease?> DequeueAsync(CancellationToken cancellationToken)
            => Task.FromResult<DiscoveryQueueLease?>(null);

        public Task<bool> EnqueueAsync(CrawlSource source, string categoryKey, string url, string itemType, int depth, string? parentUrl, string? jobId, CancellationToken cancellationToken)
        {
            Enqueued.Add((itemType, url));
            return Task.FromResult(true);
        }

        public Task<bool> EnqueueProductAsync(CrawlSource source, string categoryKey, string url, int depth, string? parentUrl, string? jobId, CancellationToken cancellationToken)
            => Task.FromResult(false);

        public Task MarkCompletedAsync(string queueItemId, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task MarkSkippedAsync(string queueItemId, string reason, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task MarkFailedAsync(string queueItemId, string reason, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class AllowAllRobotsPolicyService : IRobotsPolicyService
    {
        public Task<RobotsPolicyDecision> EvaluateAsync(CrawlTarget target, CancellationToken cancellationToken)
        {
            return Task.FromResult(new RobotsPolicyDecision
            {
                IsAllowed = true,
                Reason = "allowed"
            });
        }
    }

    private sealed class FakeDiscoveryQueueStore(params DiscoveryQueueItem[] items) : IDiscoveryQueueStore
    {
        public Dictionary<string, DiscoveryQueueItem> Items { get; } = items.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

        public Task<DiscoveryQueueItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.TryGetValue(id, out var item) ? item : null);

        public Task UpsertAsync(DiscoveryQueueItem item, CancellationToken cancellationToken = default)
        {
            Items[item.Id] = item;
            return Task.CompletedTask;
        }

        public Task<DiscoveryQueueItem?> TryAcquireAsync(string id, DateTime utcNow, CancellationToken cancellationToken = default)
        {
            if (!Items.TryGetValue(id, out var item)
                || item.State != "queued"
                || (item.NextAttemptUtc is not null && item.NextAttemptUtc > utcNow))
            {
                return Task.FromResult<DiscoveryQueueItem?>(null);
            }

            item.State = "processing";
            item.AttemptCount += 1;
            item.LastAttemptUtc = utcNow;
            item.NextAttemptUtc = null;
            item.LastError = null;
            return Task.FromResult<DiscoveryQueueItem?>(item);
        }

        public Task<long> CountActiveAsync(string sourceId, string categoryKey, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Values.LongCount(item => item.SourceId == sourceId && item.CategoryKey == categoryKey && (item.State == "queued" || item.State == "processing")));

        public Task<IReadOnlyList<DiscoveryQueueItem>> ListQueuedAsync(DateTime utcNow, CancellationToken cancellationToken = default)
        {
            var due = Items.Values
                .Where(item => item.State == "queued" && (item.NextAttemptUtc is null || item.NextAttemptUtc <= utcNow))
                .OrderBy(item => item.NextAttemptUtc)
                .ThenBy(item => item.EnqueuedUtc)
                .ToArray();
            return Task.FromResult<IReadOnlyList<DiscoveryQueueItem>>(due);
        }
    }

    private sealed class FakeDiscoveredUrlStore(params DiscoveredUrl[] items) : IDiscoveredUrlStore
    {
        public Dictionary<string, DiscoveredUrl> Items { get; } = items.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

        public Task<DiscoveredUrl?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.TryGetValue(id, out var item) ? item : null);

        public Task<DiscoveredUrl?> GetByNormalizedUrlAsync(string sourceId, string categoryKey, string normalizedUrl, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Values.FirstOrDefault(item => item.SourceId == sourceId && item.CategoryKey == categoryKey && item.NormalizedUrl == normalizedUrl));

        public Task<long> CountByScopeAsync(string sourceId, string categoryKey, string? jobId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Values.LongCount(item => item.SourceId == sourceId && item.CategoryKey == categoryKey && item.JobId == jobId));

        public Task UpsertAsync(DiscoveredUrl item, CancellationToken cancellationToken = default)
        {
            Items[item.Id] = item;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProductTargetQueueStore : IProductTargetQueueStore
    {
        private readonly Dictionary<string, CrawlQueueItem> items = new(StringComparer.OrdinalIgnoreCase);

        public Task<CrawlQueueItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            items.TryGetValue(id, out var item);
            return Task.FromResult(item);
        }

        public void Store(CrawlQueueItem item)
        {
            items[item.Id] = item;
        }
    }

    private sealed class RecordingCrawlJobQueueWriter(FakeProductTargetQueueStore queueStore) : ICrawlJobQueueWriter
    {
        public List<CrawlQueueItem> Items { get; } = [];

        public Task UpsertAsync(CrawlQueueItem item, CancellationToken cancellationToken = default)
        {
            Items.Add(item);
            queueStore.Store(item);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CrawlQueueItem>> CancelQueuedItemsAsync(string jobId, string reason, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<CrawlQueueItem>>([]);
        }
    }

    private sealed class FakeCrawlJobStore : ICrawlJobStore
    {
        public Task<CrawlJobPage> ListAsync(CrawlJobQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(new CrawlJobPage());

        public Task<CrawlJob?> GetAsync(string jobId, CancellationToken cancellationToken = default)
            => Task.FromResult<CrawlJob?>(null);

        public Task UpsertAsync(CrawlJob job, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}