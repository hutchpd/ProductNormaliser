using ProductNormaliser.Application.Crawls;
using ProductNormaliser.Application.Governance;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Tests;

public sealed class CrawlJobServiceTests
{
    [Test]
    public async Task CreateAsync_CreatesJobAndQueuesTargets()
    {
        var jobStore = new FakeCrawlJobStore();
        var queueWriter = new FakeCrawlJobQueueWriter();
        var targetStore = new FakeKnownCrawlTargetStore(
            categoryTargets:
            [
                new CrawlJobTargetDescriptor { SourceName = "currys", SourceUrl = "https://currys.example/tv-1", CategoryKey = "tv" },
                new CrawlJobTargetDescriptor { SourceName = "ao", SourceUrl = "https://ao.example/tv-2", CategoryKey = "tv" }
            ]);
        var audit = new RecordingAuditService();
        var service = new CrawlJobService(jobStore, targetStore, queueWriter, new PermissiveCrawlGovernanceService(), audit);

        var job = await service.CreateAsync(new CreateCrawlJobRequest
        {
            RequestType = CrawlJobRequestTypes.Category,
            RequestedCategories = ["tv"]
        });

        Assert.Multiple(() =>
        {
            Assert.That(job.Status, Is.EqualTo(CrawlJobStatuses.Pending));
            Assert.That(job.TotalTargets, Is.EqualTo(2));
            Assert.That(job.RequestedCategories, Is.EqualTo(new[] { "tv" }));
            Assert.That(queueWriter.Items, Has.Count.EqualTo(2));
            Assert.That(queueWriter.Items.All(item => item.JobId == job.JobId), Is.True);
            Assert.That(job.PerCategoryBreakdown.Single().TotalTargets, Is.EqualTo(2));
            Assert.That(audit.Entries.Select(entry => entry.Action), Is.EqualTo(new[] { ManagementAuditActions.CrawlJobCreated }));
        });
    }

    [Test]
    public async Task RecordTargetOutcomeAsync_UpdatesProgressAndCompletesJob()
    {
        var job = CreateJob(totalTargets: 2, categoryKey: "tv");
        var jobStore = new FakeCrawlJobStore(job);
        var service = new CrawlJobService(jobStore, new FakeKnownCrawlTargetStore(), new FakeCrawlJobQueueWriter(), new PermissiveCrawlGovernanceService(), new RecordingAuditService());

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
        var service = new CrawlJobService(jobStore, new FakeKnownCrawlTargetStore(), new FakeCrawlJobQueueWriter(), new PermissiveCrawlGovernanceService(), new RecordingAuditService());

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
        var service = new CrawlJobService(jobStore, new FakeKnownCrawlTargetStore(), new FakeCrawlJobQueueWriter(), new PermissiveCrawlGovernanceService(), new RecordingAuditService());

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
    public async Task ListAsync_NormalizesQueryAndReturnsPagedResults()
    {
        var jobStore = new FakeCrawlJobStore(
            CreateJobWithStatus("job_1", CrawlJobStatuses.Running, "tv", DateTime.UtcNow.AddMinutes(-2)),
            CreateJobWithStatus("job_2", CrawlJobStatuses.Completed, "monitor", DateTime.UtcNow.AddMinutes(-1)));
        var service = new CrawlJobService(jobStore, new FakeKnownCrawlTargetStore(), new FakeCrawlJobQueueWriter(), new PermissiveCrawlGovernanceService(), new RecordingAuditService());

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
        var service = new CrawlJobService(jobStore, new FakeKnownCrawlTargetStore(), queueWriter, new PermissiveCrawlGovernanceService(), new RecordingAuditService());

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
        var targetStore = new FakeKnownCrawlTargetStore(
            categoryTargets: Enumerable.Range(0, 6)
                .Select(index => new CrawlJobTargetDescriptor
                {
                    SourceName = $"source_{index}",
                    SourceUrl = $"https://source{index}.example/tv-{index}",
                    CategoryKey = "tv"
                })
                .ToArray());
        var governance = new FixedCrawlGovernanceService(new CrawlGovernanceOptions
        {
            MaxTargetsPerJob = 5,
            LargeCrawlThreshold = 3,
            RequireExplicitSourcesForLargeCategoryCrawls = true
        });
        var service = new CrawlJobService(jobStore, targetStore, queueWriter, governance, new RecordingAuditService());

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
        var targetStore = new FakeKnownCrawlTargetStore(
            categoryTargets:
            [
                new CrawlJobTargetDescriptor { SourceName = "ao", SourceUrl = "https://ao.example/tv-1", CategoryKey = "tv" },
                new CrawlJobTargetDescriptor { SourceName = "currys", SourceUrl = "https://currys.example/tv-2", CategoryKey = "tv" },
                new CrawlJobTargetDescriptor { SourceName = "john_lewis", SourceUrl = "https://john.example/tv-3", CategoryKey = "tv" }
            ]);
        var governance = new FixedCrawlGovernanceService(new CrawlGovernanceOptions
        {
            MaxTargetsPerJob = 10,
            LargeCrawlThreshold = 2,
            RequireExplicitSourcesForLargeCategoryCrawls = true
        });
        var service = new CrawlJobService(jobStore, targetStore, queueWriter, governance, new RecordingAuditService());

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
}