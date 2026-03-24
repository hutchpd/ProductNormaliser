using ProductNormaliser.Application.Crawls;
using ProductNormaliser.Application.Discovery;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Discovery;
using ProductNormaliser.Infrastructure.Mongo.Repositories;

namespace ProductNormaliser.Tests;

public sealed class DiscoveryQueueServiceTests
{
    [Test]
    public async Task MarkFailedAsync_RequeuesWhenRetryBudgetRemains()
    {
        var source = CreateSource(maxRetryCount: 2, retryBackoffBaseMs: 1000, retryBackoffMaxMs: 4000);
        var queueItem = new DiscoveryQueueItem
        {
            Id = "queue-1",
            JobId = "job-1",
            SourceId = source.Id,
            CategoryKey = "tv",
            Url = "https://alpha.example/category/tv?page=2",
            NormalizedUrl = "https://alpha.example/category/tv?page=2",
            Classification = "listing",
            State = "processing",
            Depth = 1,
            AttemptCount = 1,
            EnqueuedUtc = DateTime.UtcNow.AddMinutes(-5)
        };
        var discovered = new DiscoveredUrl
        {
            Id = "discovered-1",
            JobId = "job-1",
            SourceId = source.Id,
            CategoryKey = "tv",
            Url = queueItem.Url,
            NormalizedUrl = queueItem.NormalizedUrl,
            Classification = queueItem.Classification,
            State = "pending",
            Depth = 1,
            AttemptCount = 0,
            FirstSeenUtc = DateTime.UtcNow.AddMinutes(-5),
            LastSeenUtc = DateTime.UtcNow.AddMinutes(-1)
        };
        var queueStore = new FakeDiscoveryQueueStore(queueItem);
        var discoveredStore = new FakeDiscoveredUrlStore(discovered);
        var sut = CreateService(source, queueStore, discoveredStore);

        await sut.MarkFailedAsync(queueItem.Id, "timeout", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(queueStore.Items[queueItem.Id].State, Is.EqualTo("queued"));
            Assert.That(queueStore.Items[queueItem.Id].NextAttemptUtc, Is.Not.Null);
            Assert.That(queueStore.Items[queueItem.Id].CompletedUtc, Is.Null);
            Assert.That(discoveredStore.Items[discovered.Id].State, Is.EqualTo("pending"));
            Assert.That(discoveredStore.Items[discovered.Id].LastError, Is.EqualTo("timeout"));
            Assert.That(discoveredStore.Items[discovered.Id].NextAttemptUtc, Is.Not.Null);
        });
    }

    [Test]
    public async Task EnqueueAsync_RejectsNewUrlsPastJobRunBudget()
    {
        var source = CreateSource(maxUrlsPerRun: 2);
        var queueStore = new FakeDiscoveryQueueStore();
        var discoveredStore = new FakeDiscoveredUrlStore(
            new DiscoveredUrl { Id = "d1", JobId = "job-1", SourceId = source.Id, CategoryKey = "tv", Url = "https://alpha.example/category/tv", NormalizedUrl = "https://alpha.example/category/tv", Classification = "listing", State = "processed", FirstSeenUtc = DateTime.UtcNow, LastSeenUtc = DateTime.UtcNow },
            new DiscoveredUrl { Id = "d2", JobId = "job-1", SourceId = source.Id, CategoryKey = "tv", Url = "https://alpha.example/category/tv?page=2", NormalizedUrl = "https://alpha.example/category/tv?page=2", Classification = "listing", State = "processed", FirstSeenUtc = DateTime.UtcNow, LastSeenUtc = DateTime.UtcNow });
        var sut = CreateService(source, queueStore, discoveredStore);

        var enqueued = await sut.EnqueueAsync(source, "tv", "https://alpha.example/category/tv?page=3", "listing", 1, "https://alpha.example/category/tv", "job-1", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(enqueued, Is.False);
            Assert.That(queueStore.Items, Is.Empty);
        });
    }

    private static DiscoveryQueueService CreateService(CrawlSource source, FakeDiscoveryQueueStore queueStore, FakeDiscoveredUrlStore discoveredStore)
    {
        return new DiscoveryQueueService(
            queueStore,
            discoveredStore,
            new FakeCrawlSourceStore(source),
            new ProductTargetEnqueuer(new FakeProductTargetQueueStore(), new FakeCrawlJobQueueWriter(), new DiscoveryJobProgressService(new FakeCrawlJobStore())),
            new DiscoveryJobProgressService(new FakeCrawlJobStore()));
    }

    private static CrawlSource CreateSource(int maxUrlsPerRun = 500, int maxRetryCount = 3, int retryBackoffBaseMs = 1000, int retryBackoffMaxMs = 30000)
    {
        return new CrawlSource
        {
            Id = "alpha",
            DisplayName = "Alpha",
            BaseUrl = "https://alpha.example",
            Host = "alpha.example",
            DiscoveryProfile = new SourceDiscoveryProfile
            {
                MaxUrlsPerRun = maxUrlsPerRun,
                MaxRetryCount = maxRetryCount,
                RetryBackoffBaseMs = retryBackoffBaseMs,
                RetryBackoffMaxMs = retryBackoffMaxMs
            },
            ThrottlingPolicy = new SourceThrottlingPolicy()
        };
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
            => Task.FromResult<DiscoveryQueueItem?>(null);

        public Task<long> CountActiveAsync(string sourceId, string categoryKey, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Values.LongCount(item => item.SourceId == sourceId && item.CategoryKey == categoryKey && (item.State == "queued" || item.State == "processing")));

        public Task<IReadOnlyList<DiscoveryQueueItem>> ListQueuedAsync(DateTime utcNow, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DiscoveryQueueItem>>([]);
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

    private sealed class FakeCrawlSourceStore(params CrawlSource[] sources) : ICrawlSourceStore
    {
        private readonly Dictionary<string, CrawlSource> items = sources.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<CrawlSource>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CrawlSource>>(items.Values.ToArray());

        public Task<CrawlSource?> GetAsync(string sourceId, CancellationToken cancellationToken = default)
            => Task.FromResult(items.TryGetValue(sourceId, out var source) ? source : null);

        public Task UpsertAsync(CrawlSource source, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeProductTargetQueueStore : IProductTargetQueueStore
    {
        public Task<CrawlQueueItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult<CrawlQueueItem?>(null);
    }

    private sealed class FakeCrawlJobQueueWriter : ICrawlJobQueueWriter
    {
        public Task UpsertAsync(CrawlQueueItem item, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<CrawlQueueItem>> CancelQueuedItemsAsync(string jobId, string reason, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CrawlQueueItem>>([]);
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