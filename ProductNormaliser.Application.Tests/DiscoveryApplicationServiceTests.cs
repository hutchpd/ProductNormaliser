using ProductNormaliser.Application.Crawls;
using ProductNormaliser.Application.Discovery;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Discovery;

namespace ProductNormaliser.Tests;

public sealed class DiscoveryApplicationServiceTests
{
    [Test]
    public async Task SourceDiscoveryService_SeedsEnabledSourcesForRequestedCategories()
    {
        var sourceStore = new FakeCrawlSourceStore(
            CreateSource("alpha", isEnabled: true, supportedCategories: ["tv", "monitor"]),
            CreateSource("beta", isEnabled: false, supportedCategories: ["tv"]),
            CreateSource("gamma", isEnabled: true, supportedCategories: ["refrigerator"]));
        var sitemapLocator = new FakeSitemapLocator(new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["alpha"] = ["https://alpha.example/sitemap.xml"]
        });
        var seedWriter = new RecordingDiscoverySeedWriter();
        var sut = new SourceDiscoveryService(sourceStore, sitemapLocator, seedWriter, new DiscoveryLinkPolicy());

        var result = await sut.SeedAsync(["tv"], [], "job_discovery", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.SourceCount, Is.EqualTo(1));
            Assert.That(result.CategoryCount, Is.EqualTo(1));
            Assert.That(result.SeedCount, Is.EqualTo(2));
            Assert.That(seedWriter.Calls.Select(call => (call.CategoryKey, call.Url, call.Classification, call.JobId)), Is.EqualTo(new[]
            {
                ("tv", "https://alpha.example/category/tv", "listing", "job_discovery"),
                ("tv", "https://alpha.example/sitemap.xml", "sitemap", "job_discovery")
            }));
        });
    }

    [Test]
    public async Task SourceDiscoveryService_OffersSameSeedsOnSubsequentRuns()
    {
        var sourceStore = new FakeCrawlSourceStore(CreateSource("alpha", isEnabled: true, supportedCategories: ["tv"]));
        var sitemapLocator = new FakeSitemapLocator(new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["alpha"] = ["https://alpha.example/sitemap.xml"]
        });
        var seedWriter = new RecordingDiscoverySeedWriter();
        var sut = new SourceDiscoveryService(sourceStore, sitemapLocator, seedWriter, new DiscoveryLinkPolicy());

        var first = await sut.EnsureSeededAsync(CancellationToken.None);
        var second = await sut.EnsureSeededAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(first.SeedCount, Is.EqualTo(2));
            Assert.That(second.SeedCount, Is.EqualTo(2));
            Assert.That(seedWriter.Calls, Has.Count.EqualTo(4));
            Assert.That(seedWriter.Calls.Take(2).Select(call => (call.CategoryKey, call.Url, call.Classification)), Is.EqualTo(seedWriter.Calls.Skip(2).Select(call => (call.CategoryKey, call.Url, call.Classification))));
        });
    }

    [Test]
    public async Task SourceDiscoveryService_FiltersSeedsThatStronglyContradictMarketOrLocale()
    {
        var sourceStore = new FakeCrawlSourceStore(CreateSource("alpha", isEnabled: true, supportedCategories: ["tv"]));
        var sitemapLocator = new FakeSitemapLocator(new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["alpha"] =
            [
                "https://alpha.example/sitemap.xml",
                "https://alpha.example/en-us/sitemap.xml",
                "https://alpha.example/us/sitemap.xml"
            ]
        });
        var seedWriter = new RecordingDiscoverySeedWriter();
        var sut = new SourceDiscoveryService(sourceStore, sitemapLocator, seedWriter, new DiscoveryLinkPolicy());

        var result = await sut.SeedAsync(["tv"], [], "job_discovery", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.SeedCount, Is.EqualTo(2));
            Assert.That(seedWriter.Calls.Select(call => call.Url), Is.EqualTo(new[]
            {
                "https://alpha.example/category/tv",
                "https://alpha.example/sitemap.xml"
            }));
        });
    }

    [Test]
    public async Task SourceDiscoveryService_PreservesExplicitSeedOverridesForContradictoryPaths()
    {
        var source = CreateSource("alpha", isEnabled: true, supportedCategories: ["tv"]);
        source.DiscoveryProfile.CategoryEntryPages["tv"] = ["https://alpha.example/en-us/tv"];

        var sourceStore = new FakeCrawlSourceStore(source);
        var sitemapLocator = new FakeSitemapLocator(new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase));
        var seedWriter = new RecordingDiscoverySeedWriter();
        var sut = new SourceDiscoveryService(sourceStore, sitemapLocator, seedWriter, new DiscoveryLinkPolicy());

        var result = await sut.SeedAsync(["tv"], [], "job_discovery", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.SeedCount, Is.EqualTo(1));
            Assert.That(seedWriter.Calls.Select(call => call.Url), Is.EqualTo(new[] { "https://alpha.example/en-us/tv" }));
        });
    }

    [Test]
    public async Task DiscoveryJobProgressService_TracksDiscoveryCountersPerCategory()
    {
        var jobStore = new FakeCrawlJobStore(new CrawlJob
        {
            JobId = "job_discovery",
            RequestType = CrawlJobRequestTypes.Discovery,
            StartedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
            Status = CrawlJobStatuses.Pending
        });
        var sut = new DiscoveryJobProgressService(jobStore);

        await sut.RecordDiscoveredUrlAsync("job_discovery", "tv", CancellationToken.None);
        await sut.RecordDiscoveredUrlAsync("job_discovery", "tv", CancellationToken.None);
        await sut.RecordConfirmedProductAsync("job_discovery", "tv", CancellationToken.None);
        await sut.RecordProcessedPageAsync("job_discovery", "tv", "completed", CancellationToken.None);
        await sut.RecordProcessedPageAsync("job_discovery", "tv", "blocked", CancellationToken.None);

        var stored = await jobStore.GetAsync("job_discovery", CancellationToken.None);
        var breakdown = stored!.PerCategoryBreakdown.Single(item => item.CategoryKey == "tv");

        Assert.Multiple(() =>
        {
            Assert.That(stored, Is.Not.Null);
            Assert.That(stored!.Status, Is.EqualTo(CrawlJobStatuses.Completed));
            Assert.That(stored.TotalTargets, Is.EqualTo(2));
            Assert.That(stored.ProcessedTargets, Is.EqualTo(2));
            Assert.That(stored.DiscoveredUrlCount, Is.EqualTo(2));
            Assert.That(stored.ConfirmedProductCount, Is.EqualTo(1));
            Assert.That(stored.BlockedPageCount, Is.EqualTo(1));
            Assert.That(stored.RejectedPageCount, Is.EqualTo(0));
            Assert.That(stored.SuccessCount, Is.EqualTo(1));
            Assert.That(stored.SkippedCount, Is.EqualTo(1));
            Assert.That(breakdown.TotalTargets, Is.EqualTo(2));
            Assert.That(breakdown.DiscoveredUrlCount, Is.EqualTo(2));
            Assert.That(breakdown.ConfirmedProductCount, Is.EqualTo(1));
            Assert.That(breakdown.BlockedPageCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ProductTargetEnqueuer_DeduplicatesAndWritesThroughCrawlQueueWriter()
    {
        var jobStore = new FakeCrawlJobStore(new CrawlJob
        {
            JobId = "job_discovery",
            RequestType = CrawlJobRequestTypes.Discovery,
            StartedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
            Status = CrawlJobStatuses.Pending
        });
        var queueStore = new FakeProductTargetQueueStore();
        var queueWriter = new RecordingCrawlJobQueueWriter(queueStore);
        var progressService = new DiscoveryJobProgressService(jobStore);
        var sut = new ProductTargetEnqueuer(queueStore, queueWriter, progressService);
        var source = CreateSource("alpha", isEnabled: true, supportedCategories: ["tv"]);

        var first = await sut.EnqueueAsync("job_discovery", source, "tv", "https://alpha.example/product/oled-1?ref=promo", CancellationToken.None);
        var second = await sut.EnqueueAsync("job_discovery", source, "tv", "https://alpha.example/product/oled-1?ref=promo", CancellationToken.None);
        var job = await jobStore.GetAsync("job_discovery", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.True);
            Assert.That(second, Is.False);
            Assert.That(queueWriter.Items, Has.Count.EqualTo(1));
            Assert.That(queueWriter.Items[0].JobId, Is.Null);
            Assert.That(queueWriter.Items[0].InitiatingJobId, Is.EqualTo("job_discovery"));
            Assert.That(queueWriter.Items[0].Id, Does.StartWith("crawl:discovered:alpha:tv:"));
            Assert.That(job, Is.Not.Null);
            Assert.That(job!.ConfirmedProductCount, Is.EqualTo(1));
        });
    }

    private static CrawlSource CreateSource(string id, bool isEnabled, IReadOnlyList<string> supportedCategories)
    {
        return new CrawlSource
        {
            Id = id,
            DisplayName = id,
            BaseUrl = $"https://{id}.example/",
            Host = $"{id}.example",
            IsEnabled = isEnabled,
            SupportedCategoryKeys = supportedCategories.ToList(),
            DiscoveryProfile = new SourceDiscoveryProfile
            {
                CategoryEntryPages = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["tv"] = [$"https://{id}.example/category/tv"]
                }
            },
            ThrottlingPolicy = new SourceThrottlingPolicy()
        };
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
            var existingIndex = items.FindIndex(item => string.Equals(item.Id, source.Id, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                items[existingIndex] = source;
            }
            else
            {
                items.Add(source);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeSitemapLocator(IReadOnlyDictionary<string, IReadOnlyList<string>> results) : ISitemapLocator
    {
        public Task<IReadOnlyList<string>> LocateAsync(CrawlSource source, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(results.TryGetValue(source.Id, out var urls) ? urls : (IReadOnlyList<string>)[]);
        }
    }

    private sealed class RecordingDiscoverySeedWriter : IDiscoverySeedWriter
    {
        public List<DiscoverySeedCall> Calls { get; } = [];

        public Task<bool> EnqueueAsync(CrawlSource source, string categoryKey, string url, string classification, int depth, string? parentUrl, string? jobId, CancellationToken cancellationToken = default)
        {
            Calls.Add(new DiscoverySeedCall(source.Id, categoryKey, url, classification, depth, parentUrl, jobId));
            return Task.FromResult(true);
        }
    }

    private sealed record DiscoverySeedCall(string SourceId, string CategoryKey, string Url, string Classification, int Depth, string? ParentUrl, string? JobId);

    private sealed class FakeCrawlJobStore(params CrawlJob[] jobs) : ICrawlJobStore
    {
        private readonly Dictionary<string, CrawlJob> items = jobs.ToDictionary(job => job.JobId, StringComparer.OrdinalIgnoreCase);

        public Task<CrawlJobPage> ListAsync(CrawlJobQuery query, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<CrawlJob?> GetAsync(string jobId, CancellationToken cancellationToken = default)
        {
            items.TryGetValue(jobId, out var job);
            return Task.FromResult(job);
        }

        public Task UpsertAsync(CrawlJob job, CancellationToken cancellationToken = default)
        {
            items[job.JobId] = job;
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
}