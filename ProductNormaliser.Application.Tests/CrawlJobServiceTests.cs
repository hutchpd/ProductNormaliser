using ProductNormaliser.Application.Crawls;
using ProductNormaliser.Application.Discovery;
using ProductNormaliser.Application.Governance;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Tests;

public sealed class CrawlJobServiceTests
{
    [Test]
    public async Task CreateAsync_CategoryJob_SeedsDiscoveryInsteadOfProductQueue()
    {
        var jobStore = new FakeCrawlJobStore();
        var queueWriter = new FakeCrawlJobQueueWriter();
        var discoveryService = new FakeSourceDiscoveryService
        {
            Preview = new SourceDiscoveryPreview
            {
                SourceCount = 2,
                CategoryCount = 1,
                Seeds =
                [
                    new SourceDiscoverySeedDescriptor { SourceId = "currys", CategoryKey = "tv", Url = "https://currys.example/category/tv", Classification = "listing" },
                    new SourceDiscoverySeedDescriptor { SourceId = "ao", CategoryKey = "tv", Url = "https://ao.example/sitemap.xml", Classification = "sitemap" }
                ]
            },
            SeedResult = new SourceDiscoverySeedResult { SourceCount = 2, CategoryCount = 1, SeedCount = 2 },
            OnSeedAsync = async jobId =>
            {
                var storedJob = await jobStore.GetAsync(jobId!, CancellationToken.None);
                storedJob!.TotalTargets = 2;
                storedJob.DiscoveredUrlCount = 2;
                storedJob.PerCategoryBreakdown.Single().TotalTargets = 2;
                storedJob.PerCategoryBreakdown.Single().DiscoveredUrlCount = 2;
                await jobStore.UpsertAsync(storedJob, CancellationToken.None);
            }
        };
        var audit = new RecordingAuditService();
        var service = new CrawlJobService(jobStore, new FakeKnownCrawlTargetStore(), discoveryService, queueWriter, new PermissiveCrawlGovernanceService(), audit);

        var job = await service.CreateAsync(new CreateCrawlJobRequest
        {
            RequestType = CrawlJobRequestTypes.Category,
            RequestedCategories = ["tv"]
        });

        Assert.Multiple(() =>
        {
            Assert.That(job.Status, Is.EqualTo(CrawlJobStatuses.Pending));
            Assert.That(job.TotalTargets, Is.EqualTo(2));
            Assert.That(job.DiscoveredUrlCount, Is.EqualTo(2));
            Assert.That(job.RequestedCategories, Is.EqualTo(new[] { "tv" }));
            Assert.That(queueWriter.Items, Is.Empty);
            Assert.That(discoveryService.SeedCalls, Has.Count.EqualTo(1));
            Assert.That(discoveryService.SeedCalls[0].JobId, Is.EqualTo(job.JobId));
            Assert.That(job.PerCategoryBreakdown.Single().TotalTargets, Is.EqualTo(2));
            Assert.That(audit.Entries.Select(entry => entry.Action), Is.EqualTo(new[] { ManagementAuditActions.CrawlJobCreated }));
        });
    }

    [Test]
    public async Task CreateAsync_ProductSelectionJob_KeepsKnownTargetBehaviour()
    {
        var jobStore = new FakeCrawlJobStore();
        var queueWriter = new FakeCrawlJobQueueWriter();
        var targetStore = new FakeKnownCrawlTargetStore(
            productTargets:
            [
                new CrawlJobTargetDescriptor { SourceName = "currys", SourceUrl = "https://currys.example/tv-1", CategoryKey = "tv" },
                new CrawlJobTargetDescriptor { SourceName = "currys", SourceUrl = "https://currys.example/tv-1", CategoryKey = "tv" },
                new CrawlJobTargetDescriptor { SourceName = "ao", SourceUrl = "https://ao.example/monitor-1", CategoryKey = "monitor" }
            ]);
        var service = new CrawlJobService(jobStore, targetStore, new FakeSourceDiscoveryService(), queueWriter, new PermissiveCrawlGovernanceService(), new RecordingAuditService());

        var job = await service.CreateAsync(new CreateCrawlJobRequest
        {
            RequestType = CrawlJobRequestTypes.ProductSelection,
            RequestedProductIds = ["tv-1", "monitor-1"]
        });

        Assert.Multiple(() =>
        {
            Assert.That(job.TotalTargets, Is.EqualTo(2));
            Assert.That(queueWriter.Items, Has.Count.EqualTo(2));
            Assert.That(job.PerCategoryBreakdown.Select(item => (item.CategoryKey, item.TotalTargets)), Is.EqualTo(new[]
            {
                ("monitor", 1),
                ("tv", 1)
            }));
        });
    }

    [Test]
    public async Task CreateAsync_CategoryJob_BuildsDistinctBreakdownsAcrossMultipleCategories()
    {
        var jobStore = new FakeCrawlJobStore();
        var queueWriter = new FakeCrawlJobQueueWriter();
        var discoveryService = new FakeSourceDiscoveryService
        {
            Preview = new SourceDiscoveryPreview
            {
                SourceCount = 2,
                CategoryCount = 2,
                Seeds =
                [
                    new SourceDiscoverySeedDescriptor { SourceId = "currys", CategoryKey = "tv", Url = "https://currys.example/category/tv", Classification = "listing" },
                    new SourceDiscoverySeedDescriptor { SourceId = "ao", CategoryKey = "monitor", Url = "https://ao.example/category/monitor", Classification = "listing" }
                ]
            },
            SeedResult = new SourceDiscoverySeedResult { SourceCount = 2, CategoryCount = 2, SeedCount = 2 },
            OnSeedAsync = async jobId =>
            {
                var storedJob = await jobStore.GetAsync(jobId!, CancellationToken.None);
                storedJob!.TotalTargets = 2;
                storedJob.DiscoveredUrlCount = 2;
                storedJob.PerCategoryBreakdown.Single(item => item.CategoryKey == "tv").TotalTargets = 1;
                storedJob.PerCategoryBreakdown.Single(item => item.CategoryKey == "tv").DiscoveredUrlCount = 1;
                storedJob.PerCategoryBreakdown.Single(item => item.CategoryKey == "monitor").TotalTargets = 1;
                storedJob.PerCategoryBreakdown.Single(item => item.CategoryKey == "monitor").DiscoveredUrlCount = 1;
                await jobStore.UpsertAsync(storedJob, CancellationToken.None);
            }
        };
        var service = new CrawlJobService(jobStore, new FakeKnownCrawlTargetStore(), discoveryService, queueWriter, new PermissiveCrawlGovernanceService(), new RecordingAuditService());

        var job = await service.CreateAsync(new CreateCrawlJobRequest
        {
            RequestType = CrawlJobRequestTypes.Category,
            RequestedCategories = ["tv", "monitor"]
        });

        Assert.Multiple(() =>
        {
            Assert.That(job.TotalTargets, Is.EqualTo(2));
            Assert.That(queueWriter.Items, Is.Empty);
            Assert.That(job.PerCategoryBreakdown.Select(item => (item.CategoryKey, item.TotalTargets)), Is.EqualTo(new[]
            {
                ("monitor", 1),
                ("tv", 1)
            }));
        });
    }

    [Test]
    public async Task RecordTargetOutcomeAsync_UpdatesProgressAndCompletesJob()
    {
        var job = CreateJob(totalTargets: 2, categoryKey: "tv");
        var jobStore = new FakeCrawlJobStore(job);
        var service = new CrawlJobService(jobStore, new FakeKnownCrawlTargetStore(), new FakeSourceDiscoveryService(), new FakeCrawlJobQueueWriter(), new PermissiveCrawlGovernanceService(), new RecordingAuditService());

        await service.MarkStartedAsync(job.JobId);
        await service.RecordTargetOutcomeAsync(job.JobId, "tv", "completed");
        await service.RecordTargetOutcomeAsync(job.JobId, "tv", "skipped");

        var stored = await jobStore.GetAsync(job.JobId);

        Assert.Multiple(() =>
        {
            Assert.That(stored, Is.Not.Null);
            Assert.That(stored!.ProcessedTargets, Is.EqualTo(2));
            Assert.That(stored.SuccessCount, Is.EqualTo(1));
            Assert.That(stored.SkippedCount, Is.EqualTo(1));
            Assert.That(stored.FailedCount, Is.EqualTo(0));
            Assert.That(stored.Status, Is.EqualTo(CrawlJobStatuses.Completed));
            Assert.That(stored.EstimatedCompletion, Is.Not.Null);
            Assert.That(stored.PerCategoryBreakdown.Single().ProcessedTargets, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task RecordTargetOutcomeAsync_MarksCompletedWithFailuresForMixedResults()
    {
        var job = CreateJob(totalTargets: 2, categoryKey: "tv");
        var jobStore = new FakeCrawlJobStore(job);
        var service = new CrawlJobService(jobStore, new FakeKnownCrawlTargetStore(), new FakeSourceDiscoveryService(), new FakeCrawlJobQueueWriter(), new PermissiveCrawlGovernanceService(), new RecordingAuditService());

        await service.RecordTargetOutcomeAsync(job.JobId, "tv", "completed");
        await service.RecordTargetOutcomeAsync(job.JobId, "tv", "failed");

        var stored = await jobStore.GetAsync(job.JobId);

        Assert.Multiple(() =>
        {
            Assert.That(stored, Is.Not.Null);
            Assert.That(stored!.Status, Is.EqualTo(CrawlJobStatuses.CompletedWithFailures));
            Assert.That(stored.FailedCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task RecordTargetOutcomeAsync_MarksJobFailedWhenAllTargetsFail()
    {
        var job = CreateJob(totalTargets: 2, categoryKey: "tv");
        var jobStore = new FakeCrawlJobStore(job);
        var service = new CrawlJobService(jobStore, new FakeKnownCrawlTargetStore(), new FakeSourceDiscoveryService(), new FakeCrawlJobQueueWriter(), new PermissiveCrawlGovernanceService(), new RecordingAuditService());

        await service.RecordTargetOutcomeAsync(job.JobId, "tv", "failed");
        await service.RecordTargetOutcomeAsync(job.JobId, "tv", "failed");

        var stored = await jobStore.GetAsync(job.JobId);

        Assert.Multiple(() =>
        {
            Assert.That(stored, Is.Not.Null);
            Assert.That(stored!.Status, Is.EqualTo(CrawlJobStatuses.Failed));
            Assert.That(stored.FailedCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task CrawlJobLifecycle_EmitsStructuredLogs()
    {
        var jobStore = new FakeCrawlJobStore();
        var queueWriter = new FakeCrawlJobQueueWriter();
        var discoveryService = new FakeSourceDiscoveryService
        {
            Preview = new SourceDiscoveryPreview
            {
                SourceCount = 1,
                CategoryCount = 1,
                Seeds = [new SourceDiscoverySeedDescriptor { SourceId = "currys", CategoryKey = "tv", Url = "https://currys.example/category/tv", Classification = "listing" }]
            },
            SeedResult = new SourceDiscoverySeedResult { SourceCount = 1, CategoryCount = 1, SeedCount = 1 },
            OnSeedAsync = async jobId =>
            {
                var storedJob = await jobStore.GetAsync(jobId!, CancellationToken.None);
                storedJob!.TotalTargets = 1;
                storedJob.DiscoveredUrlCount = 1;
                storedJob.PerCategoryBreakdown.Single().TotalTargets = 1;
                storedJob.PerCategoryBreakdown.Single().DiscoveredUrlCount = 1;
                await jobStore.UpsertAsync(storedJob, CancellationToken.None);
            }
        };
        var logger = new TestLogger<CrawlJobService>();
        var service = new CrawlJobService(jobStore, new FakeKnownCrawlTargetStore(), discoveryService, queueWriter, new PermissiveCrawlGovernanceService(), new RecordingAuditService(), logger);

        var job = await service.CreateAsync(new CreateCrawlJobRequest
        {
            RequestType = CrawlJobRequestTypes.Category,
            RequestedCategories = ["tv"]
        });

        await service.MarkStartedAsync(job.JobId);
        await service.RecordTargetOutcomeAsync(job.JobId, "tv", "completed");

        Assert.That(logger.Entries.Select(entry => entry.Message), Has.Some.Contains("Created discovery-seeded crawl job"));
        Assert.That(logger.Entries.Select(entry => entry.Message), Has.Some.Contains("Started crawl job"));
        Assert.That(logger.Entries.Select(entry => entry.Message), Has.Some.Contains("Recorded crawl job outcome completed"));
        Assert.That(logger.Entries.Select(entry => entry.Message), Has.Some.Contains("reached terminal status completed"));
    }

    [Test]
    public async Task RecordTargetOutcomeAsync_IgnoresDuplicateOutcomeOnceJobIsComplete()
    {
        var job = CreateJob(totalTargets: 1, categoryKey: "tv");
        var jobStore = new FakeCrawlJobStore(job);
        var service = new CrawlJobService(jobStore, new FakeKnownCrawlTargetStore(), new FakeSourceDiscoveryService(), new FakeCrawlJobQueueWriter(), new PermissiveCrawlGovernanceService(), new RecordingAuditService());

        await service.RecordTargetOutcomeAsync(job.JobId, "tv", "completed");
        await service.RecordTargetOutcomeAsync(job.JobId, "tv", "failed");

        var stored = await jobStore.GetAsync(job.JobId);

        Assert.Multiple(() =>
        {
            Assert.That(stored, Is.Not.Null);
            Assert.That(stored!.ProcessedTargets, Is.EqualTo(1));
            Assert.That(stored.SuccessCount, Is.EqualTo(1));
            Assert.That(stored.FailedCount, Is.EqualTo(0));
            Assert.That(stored.Status, Is.EqualTo(CrawlJobStatuses.Completed));
        });
    }

    [Test]
    public async Task ListAsync_NormalizesQueryAndReturnsPagedResults()
    {
        var jobStore = new FakeCrawlJobStore(
            CreateJobWithStatus("job_1", CrawlJobStatuses.Running, "tv", DateTime.UtcNow.AddMinutes(-2)),
            CreateJobWithStatus("job_2", CrawlJobStatuses.Completed, "monitor", DateTime.UtcNow.AddMinutes(-1)));
        var service = new CrawlJobService(jobStore, new FakeKnownCrawlTargetStore(), new FakeSourceDiscoveryService(), new FakeCrawlJobQueueWriter(), new PermissiveCrawlGovernanceService(), new RecordingAuditService());

        var result = await service.ListAsync(new CrawlJobQuery
        {
            Status = "running",
            CategoryKey = "tv",
            Page = 0,
            PageSize = 500
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Page, Is.EqualTo(1));
            Assert.That(result.PageSize, Is.EqualTo(100));
            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Items.Select(job => job.JobId), Is.EqualTo(new[] { "job_1" }));
        });
    }

    [Test]
    public async Task CancelAsync_CancelsQueuedTargetsAndMarksJobCancelledWhenNoActiveWorkRemains()
    {
        var job = CreateJob(totalTargets: 2, categoryKey: "tv");
        var queueWriter = new FakeCrawlJobQueueWriter
        {
            CancelledItems =
            [
                new CrawlQueueItem { Id = "job_1:1", JobId = "job_1", CategoryKey = "tv", Status = "cancelled" },
                new CrawlQueueItem { Id = "job_1:2", JobId = "job_1", CategoryKey = "tv", Status = "cancelled" }
            ]
        };
        var jobStore = new FakeCrawlJobStore(job);
        var service = new CrawlJobService(jobStore, new FakeKnownCrawlTargetStore(), new FakeSourceDiscoveryService(), queueWriter, new PermissiveCrawlGovernanceService(), new RecordingAuditService());

        var cancelledJob = await service.CancelAsync(job.JobId);

        Assert.Multiple(() =>
        {
            Assert.That(cancelledJob, Is.Not.Null);
            Assert.That(cancelledJob!.Status, Is.EqualTo(CrawlJobStatuses.Cancelled));
            Assert.That(cancelledJob.CancelledCount, Is.EqualTo(2));
            Assert.That(cancelledJob.ProcessedTargets, Is.EqualTo(2));
            Assert.That(cancelledJob.PerCategoryBreakdown.Single().CancelledCount, Is.EqualTo(2));
        });
    }

    [Test]
    public void CreateAsync_RejectsOversizedCrawlRequests()
    {
        var jobStore = new FakeCrawlJobStore();
        var queueWriter = new FakeCrawlJobQueueWriter();
        var discoveryService = new FakeSourceDiscoveryService
        {
            Preview = new SourceDiscoveryPreview
            {
                SourceCount = 6,
                CategoryCount = 1,
                Seeds = Enumerable.Range(0, 6)
                    .Select(index => new SourceDiscoverySeedDescriptor
                    {
                        SourceId = $"source_{index}",
                        CategoryKey = "tv",
                        Url = $"https://source{index}.example/category/tv",
                        Classification = "listing"
                    })
                    .ToArray()
            }
        };
        var governance = new FixedCrawlGovernanceService(new CrawlGovernanceOptions
        {
            MaxTargetsPerJob = 5,
            LargeCrawlThreshold = 3,
            RequireExplicitSourcesForLargeCategoryCrawls = true
        });
        var service = new CrawlJobService(jobStore, new FakeKnownCrawlTargetStore(), discoveryService, queueWriter, governance, new RecordingAuditService());

        var action = async () => await service.CreateAsync(new CreateCrawlJobRequest
        {
            RequestType = CrawlJobRequestTypes.Category,
            RequestedCategories = ["tv"]
        });

        Assert.That(action, Throws.ArgumentException.With.Message.Contain("exceeds the configured maximum"));
    }

    [Test]
    public void CreateAsync_RequiresExplicitSourcesForLargeCategoryWideCrawls()
    {
        var jobStore = new FakeCrawlJobStore();
        var queueWriter = new FakeCrawlJobQueueWriter();
        var discoveryService = new FakeSourceDiscoveryService
        {
            Preview = new SourceDiscoveryPreview
            {
                SourceCount = 3,
                CategoryCount = 1,
                Seeds =
                [
                    new SourceDiscoverySeedDescriptor { SourceId = "ao", CategoryKey = "tv", Url = "https://ao.example/category/tv", Classification = "listing" },
                    new SourceDiscoverySeedDescriptor { SourceId = "currys", CategoryKey = "tv", Url = "https://currys.example/category/tv", Classification = "listing" },
                    new SourceDiscoverySeedDescriptor { SourceId = "john_lewis", CategoryKey = "tv", Url = "https://john.example/category/tv", Classification = "listing" }
                ]
            }
        };
        var governance = new FixedCrawlGovernanceService(new CrawlGovernanceOptions
        {
            MaxTargetsPerJob = 10,
            LargeCrawlThreshold = 2,
            RequireExplicitSourcesForLargeCategoryCrawls = true
        });
        var service = new CrawlJobService(jobStore, new FakeKnownCrawlTargetStore(), discoveryService, queueWriter, governance, new RecordingAuditService());

        var action = async () => await service.CreateAsync(new CreateCrawlJobRequest
        {
            RequestType = CrawlJobRequestTypes.Category,
            RequestedCategories = ["tv"]
        });

        Assert.That(action, Throws.ArgumentException.With.Message.Contain("Select one or more sources"));
    }

    private static CrawlJob CreateJob(int totalTargets, string categoryKey)
    {
        return new CrawlJob
        {
            JobId = "job_1",
            RequestType = CrawlJobRequestTypes.Category,
            RequestedCategories = [categoryKey],
            TotalTargets = totalTargets,
            StartedAt = DateTime.UtcNow.AddMinutes(-2),
            LastUpdatedAt = DateTime.UtcNow.AddMinutes(-2),
            Status = CrawlJobStatuses.Pending,
            PerCategoryBreakdown =
            [
                new CrawlJobCategoryBreakdown
                {
                    CategoryKey = categoryKey,
                    TotalTargets = totalTargets
                }
            ]
        };
    }

    private static CrawlJob CreateJobWithStatus(string jobId, string status, string categoryKey, DateTime lastUpdatedAt)
    {
        return new CrawlJob
        {
            JobId = jobId,
            RequestType = CrawlJobRequestTypes.Category,
            RequestedCategories = [categoryKey],
            TotalTargets = 2,
            StartedAt = lastUpdatedAt.AddMinutes(-1),
            LastUpdatedAt = lastUpdatedAt,
            Status = status,
            PerCategoryBreakdown =
            [
                new CrawlJobCategoryBreakdown
                {
                    CategoryKey = categoryKey,
                    TotalTargets = 2
                }
            ]
        };
    }

    private sealed class FakeCrawlJobStore(params CrawlJob[] jobs) : ICrawlJobStore
    {
        private readonly List<CrawlJob> items = jobs.ToList();

        public Task<CrawlJobPage> ListAsync(CrawlJobQuery query, CancellationToken cancellationToken = default)
        {
            IEnumerable<CrawlJob> filtered = items;

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                filtered = filtered.Where(job => string.Equals(job.Status, query.Status, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query.CategoryKey))
            {
                filtered = filtered.Where(job => job.RequestedCategories.Contains(query.CategoryKey, StringComparer.OrdinalIgnoreCase));
            }

            var ordered = filtered.OrderByDescending(job => job.LastUpdatedAt).ToArray();
            var pageItems = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArray();

            return Task.FromResult(new CrawlJobPage
            {
                Items = pageItems,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = ordered.LongLength
            });
        }

        public Task<CrawlJob?> GetAsync(string jobId, CancellationToken cancellationToken = default)
            => Task.FromResult(items.FirstOrDefault(job => string.Equals(job.JobId, jobId, StringComparison.OrdinalIgnoreCase)));

        public Task UpsertAsync(CrawlJob job, CancellationToken cancellationToken = default)
        {
            items.RemoveAll(existing => string.Equals(existing.JobId, job.JobId, StringComparison.OrdinalIgnoreCase));
            items.Add(job);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeKnownCrawlTargetStore(
        IReadOnlyList<CrawlJobTargetDescriptor>? categoryTargets = null,
        IReadOnlyList<CrawlJobTargetDescriptor>? productTargets = null) : IKnownCrawlTargetStore
    {
        public Task<IReadOnlyList<CrawlJobTargetDescriptor>> ListKnownTargetsAsync(IReadOnlyCollection<string> categoryKeys, IReadOnlyCollection<string> sourceNames, CancellationToken cancellationToken = default)
            => Task.FromResult(categoryTargets ?? (IReadOnlyList<CrawlJobTargetDescriptor>)[]);

        public Task<IReadOnlyList<CrawlJobTargetDescriptor>> ListTargetsForProductsAsync(IReadOnlyCollection<string> productIds, CancellationToken cancellationToken = default)
            => Task.FromResult(productTargets ?? (IReadOnlyList<CrawlJobTargetDescriptor>)[]);
    }

    private sealed class FakeCrawlJobQueueWriter : ICrawlJobQueueWriter
    {
        public List<CrawlQueueItem> Items { get; } = [];

        public IReadOnlyList<CrawlQueueItem> CancelledItems { get; init; } = [];

        public Task UpsertAsync(CrawlQueueItem item, CancellationToken cancellationToken = default)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CrawlQueueItem>> CancelQueuedItemsAsync(string jobId, string reason, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CancelledItems);
        }
    }

    private sealed class RecordingAuditService : IManagementAuditService
    {
        public List<ManagementAuditEntry> Entries { get; } = [];

        public Task RecordAsync(string action, string targetType, string targetId, IReadOnlyDictionary<string, string>? details = null, CancellationToken cancellationToken = default)
        {
            Entries.Add(new ManagementAuditEntry
            {
                Action = action,
                TargetType = targetType,
                TargetId = targetId,
                Details = details is null ? [] : new Dictionary<string, string>(details, StringComparer.OrdinalIgnoreCase)
            });
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ManagementAuditEntry>> ListRecentAsync(int take = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ManagementAuditEntry>>(Entries.Take(take).ToArray());
    }

    private sealed class PermissiveCrawlGovernanceService : ICrawlGovernanceService
    {
        public void ValidateSourceBaseUrl(string baseUrl, string parameterName)
        {
        }

        public void ValidateCrawlRequest(string requestType, IReadOnlyCollection<string> categories, IReadOnlyCollection<string> sources, IReadOnlyCollection<string> productIds, IReadOnlyCollection<CrawlJobTargetDescriptor> targets, string parameterName)
        {
        }
    }

    private sealed class FixedCrawlGovernanceService(CrawlGovernanceOptions options) : ICrawlGovernanceService
    {
        private readonly CrawlGovernanceService inner = new(Microsoft.Extensions.Options.Options.Create(options));

        public void ValidateSourceBaseUrl(string baseUrl, string parameterName) => inner.ValidateSourceBaseUrl(baseUrl, parameterName);

        public void ValidateCrawlRequest(string requestType, IReadOnlyCollection<string> categories, IReadOnlyCollection<string> sources, IReadOnlyCollection<string> productIds, IReadOnlyCollection<CrawlJobTargetDescriptor> targets, string parameterName)
            => inner.ValidateCrawlRequest(requestType, categories, sources, productIds, targets, parameterName);
    }

    private sealed class FakeSourceDiscoveryService : ISourceDiscoveryService
    {
        public SourceDiscoveryPreview Preview { get; init; } = new();

        public SourceDiscoverySeedResult SeedResult { get; init; } = new();

        public Func<string?, Task>? OnSeedAsync { get; init; }

        public List<(IReadOnlyCollection<string>? Categories, IReadOnlyCollection<string>? Sources, string? JobId)> SeedCalls { get; } = [];

        public Task<SourceDiscoveryPreview> PreviewAsync(IReadOnlyCollection<string>? categoryKeys, IReadOnlyCollection<string>? sourceIds, CancellationToken cancellationToken = default)
            => Task.FromResult(Preview);

        public Task<SourceDiscoverySeedResult> EnsureSeededAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(SeedResult);

        public async Task<SourceDiscoverySeedResult> SeedAsync(IReadOnlyCollection<string>? categoryKeys, IReadOnlyCollection<string>? sourceIds, string? jobId, CancellationToken cancellationToken = default)
        {
            SeedCalls.Add((categoryKeys, sourceIds, jobId));
            if (OnSeedAsync is not null)
            {
                await OnSeedAsync(jobId);
            }

            return SeedResult;
        }
    }
}