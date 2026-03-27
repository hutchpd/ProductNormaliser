using Microsoft.Extensions.Options;
using ProductNormaliser.Application.Crawls;
using ProductNormaliser.Application.Discovery;
using ProductNormaliser.Application.Governance;
using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Crawling;
using ProductNormaliser.Infrastructure.Mongo;
using ProductNormaliser.Infrastructure.Mongo.Repositories;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.CrawlOrchestration)]
public sealed class CrawlQueueServiceJobTests
{
    [SetUp]
    public async Task SetUpAsync()
    {
        var context = MongoIntegrationTestFixture.Context;
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.CrawlJobs);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.CrawlQueue);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.CrawlLogs);
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.SourceQualitySnapshots);
        await context.EnsureIndexesAsync();
    }

    [Test]
    public async Task DequeueAsync_MarksJobStartedAndPropagatesLeaseMetadata()
    {
        var context = MongoIntegrationTestFixture.Context;
        var queueStore = new CrawlQueueRepository(context);
        var jobStore = new CrawlJobRepository(context);
        var jobService = new CrawlJobService(jobStore, new EmptyKnownCrawlTargetStore(), new NoOpSourceDiscoveryService(), queueStore, new PermissiveCrawlGovernanceService(), new NullManagementAuditService());
        await jobStore.UpsertAsync(CreateJob("job_1", totalTargets: 1));
        await queueStore.UpsertAsync(new CrawlQueueItem
        {
            Id = "queue-1",
            JobId = "job_1",
            SourceName = "currys",
            SourceUrl = "https://currys.example/tv-1",
            CategoryKey = "tv",
            Status = "queued",
            EnqueuedUtc = DateTime.UtcNow,
            NextAttemptUtc = DateTime.UtcNow
        });

        var service = CreateService(context, queueStore, jobService);

        var lease = await service.DequeueAsync(CancellationToken.None);
        var storedJob = await jobStore.GetAsync("job_1");

        Assert.Multiple(() =>
        {
            Assert.That(lease, Is.Not.Null);
            Assert.That(storedJob, Is.Not.Null);
            Assert.That(storedJob!.Status, Is.EqualTo(CrawlJobStatuses.Running));
            Assert.That(lease!.Target.Metadata["queueItemId"], Is.EqualTo("queue-1"));
        });
    }

    [Test]
    public async Task MarkCompletedAsync_FinalizesJobQueueItemAndCompletesJob()
    {
        var context = MongoIntegrationTestFixture.Context;
        var queueStore = new CrawlQueueRepository(context);
        var jobStore = new CrawlJobRepository(context);
        var jobService = new CrawlJobService(jobStore, new EmptyKnownCrawlTargetStore(), new NoOpSourceDiscoveryService(), queueStore, new PermissiveCrawlGovernanceService(), new NullManagementAuditService());
        await jobStore.UpsertAsync(CreateJob("job_1", totalTargets: 1));
        await queueStore.UpsertAsync(new CrawlQueueItem
        {
            Id = "queue-1",
            JobId = "job_1",
            SourceName = "currys",
            SourceUrl = "https://currys.example/tv-1",
            CategoryKey = "tv",
            Status = "queued",
            EnqueuedUtc = DateTime.UtcNow,
            NextAttemptUtc = DateTime.UtcNow
        });

        var service = CreateService(context, queueStore, jobService);

        await service.DequeueAsync(CancellationToken.None);

        await service.MarkCompletedAsync("queue-1", CancellationToken.None);

        var queueItem = await queueStore.GetByIdAsync("queue-1");
        var job = await jobStore.GetAsync("job_1");

        Assert.Multiple(() =>
        {
            Assert.That(queueItem, Is.Not.Null);
            Assert.That(queueItem!.Status, Is.EqualTo("completed"));
            Assert.That(queueItem.NextAttemptUtc, Is.Null);
            Assert.That(job, Is.Not.Null);
            Assert.That(job!.Status, Is.EqualTo(CrawlJobStatuses.Completed));
            Assert.That(job.ProcessedTargets, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task MarkCompletedAsync_IsIdempotentForCompletedJobItems()
    {
        var context = MongoIntegrationTestFixture.Context;
        var queueStore = new CrawlQueueRepository(context);
        var jobStore = new CrawlJobRepository(context);
        var jobService = new CrawlJobService(jobStore, new EmptyKnownCrawlTargetStore(), new NoOpSourceDiscoveryService(), queueStore, new PermissiveCrawlGovernanceService(), new NullManagementAuditService());
        await jobStore.UpsertAsync(CreateJob("job_1", totalTargets: 1));
        await queueStore.UpsertAsync(new CrawlQueueItem
        {
            Id = "queue-1",
            JobId = "job_1",
            SourceName = "currys",
            SourceUrl = "https://currys.example/tv-1",
            CategoryKey = "tv",
            Status = "queued",
            EnqueuedUtc = DateTime.UtcNow,
            NextAttemptUtc = DateTime.UtcNow
        });

        var service = CreateService(context, queueStore, jobService);

        await service.DequeueAsync(CancellationToken.None);
        await service.MarkCompletedAsync("queue-1", CancellationToken.None);
        await service.MarkFailedAsync("queue-1", "late duplicate failure", CancellationToken.None);
        await service.MarkCompletedAsync("queue-1", CancellationToken.None);

        var queueItem = await queueStore.GetByIdAsync("queue-1");
        var job = await jobStore.GetAsync("job_1");

        Assert.Multiple(() =>
        {
            Assert.That(queueItem, Is.Not.Null);
            Assert.That(queueItem!.Status, Is.EqualTo("completed"));
            Assert.That(job, Is.Not.Null);
            Assert.That(job!.ProcessedTargets, Is.EqualTo(1));
            Assert.That(job.SuccessCount, Is.EqualTo(1));
            Assert.That(job.FailedCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task MarkFailedAsync_RetriesJobItemsBeforeFinalFailure()
    {
        var context = MongoIntegrationTestFixture.Context;
        var queueStore = new CrawlQueueRepository(context);
        var jobStore = new CrawlJobRepository(context);
        var jobService = new CrawlJobService(jobStore, new EmptyKnownCrawlTargetStore(), new NoOpSourceDiscoveryService(), queueStore, new PermissiveCrawlGovernanceService(), new NullManagementAuditService());
        await jobStore.UpsertAsync(CreateJob("job_retry", totalTargets: 1));
        await queueStore.UpsertAsync(new CrawlQueueItem
        {
            Id = "queue-retry",
            JobId = "job_retry",
            SourceName = "currys",
            SourceUrl = "https://currys.example/tv-1",
            CategoryKey = "tv",
            Status = "queued",
            EnqueuedUtc = DateTime.UtcNow,
            NextAttemptUtc = DateTime.UtcNow
        });

        var service = CreateService(context, queueStore, jobService, transientRetryCount: 2);

        await service.DequeueAsync(CancellationToken.None);
        await service.MarkFailedAsync("queue-retry", "timeout-1", CancellationToken.None);

        var firstRetryQueueItem = await queueStore.GetByIdAsync("queue-retry");
        var firstRetryJob = await jobStore.GetAsync("job_retry");

        Assert.Multiple(() =>
        {
            Assert.That(firstRetryQueueItem, Is.Not.Null);
            Assert.That(firstRetryQueueItem!.Status, Is.EqualTo("queued"));
            Assert.That(firstRetryQueueItem.AttemptCount, Is.EqualTo(1));
            Assert.That(firstRetryQueueItem.ConsecutiveFailureCount, Is.EqualTo(1));
            Assert.That(firstRetryQueueItem.NextAttemptUtc, Is.Not.Null);
            Assert.That(firstRetryJob, Is.Not.Null);
            Assert.That(firstRetryJob!.ProcessedTargets, Is.EqualTo(0));
            Assert.That(firstRetryJob.FailedCount, Is.EqualTo(0));
            Assert.That(firstRetryJob.Status, Is.EqualTo(CrawlJobStatuses.Running));
        });

        await service.DequeueAsync(CancellationToken.None);
        await service.MarkFailedAsync("queue-retry", "timeout-2", CancellationToken.None);

        var secondRetryQueueItem = await queueStore.GetByIdAsync("queue-retry");
        Assert.Multiple(() =>
        {
            Assert.That(secondRetryQueueItem, Is.Not.Null);
            Assert.That(secondRetryQueueItem!.Status, Is.EqualTo("queued"));
            Assert.That(secondRetryQueueItem.AttemptCount, Is.EqualTo(2));
            Assert.That(secondRetryQueueItem.ConsecutiveFailureCount, Is.EqualTo(2));
        });

        await service.DequeueAsync(CancellationToken.None);
        await service.MarkFailedAsync("queue-retry", "timeout-3", CancellationToken.None);

        var finalQueueItem = await queueStore.GetByIdAsync("queue-retry");
        var finalJob = await jobStore.GetAsync("job_retry");

        Assert.Multiple(() =>
        {
            Assert.That(finalQueueItem, Is.Not.Null);
            Assert.That(finalQueueItem!.Status, Is.EqualTo("failed"));
            Assert.That(finalQueueItem.AttemptCount, Is.EqualTo(3));
            Assert.That(finalQueueItem.ConsecutiveFailureCount, Is.EqualTo(3));
            Assert.That(finalQueueItem.NextAttemptUtc, Is.Null);
            Assert.That(finalJob, Is.Not.Null);
            Assert.That(finalJob!.ProcessedTargets, Is.EqualTo(1));
            Assert.That(finalJob.FailedCount, Is.EqualTo(1));
            Assert.That(finalJob.Status, Is.EqualTo(CrawlJobStatuses.Failed));
        });
    }

    [Test]
    public async Task MixedOutcomes_FinalizeJobAsCompletedWithFailures()
    {
        var context = MongoIntegrationTestFixture.Context;
        var queueStore = new CrawlQueueRepository(context);
        var jobStore = new CrawlJobRepository(context);
        var jobService = new CrawlJobService(jobStore, new EmptyKnownCrawlTargetStore(), new NoOpSourceDiscoveryService(), queueStore, new PermissiveCrawlGovernanceService(), new NullManagementAuditService());
        await jobStore.UpsertAsync(CreateJob("job_partial", totalTargets: 2));
        await queueStore.UpsertAsync(new CrawlQueueItem
        {
            Id = "queue-success",
            JobId = "job_partial",
            SourceName = "currys",
            SourceUrl = "https://currys.example/tv-1",
            CategoryKey = "tv",
            Status = "queued",
            EnqueuedUtc = DateTime.UtcNow.AddMinutes(-1),
            NextAttemptUtc = DateTime.UtcNow.AddMinutes(-1)
        });
        await queueStore.UpsertAsync(new CrawlQueueItem
        {
            Id = "queue-failure",
            JobId = "job_partial",
            SourceName = "ao",
            SourceUrl = "https://ao.example/tv-2",
            CategoryKey = "tv",
            Status = "queued",
            EnqueuedUtc = DateTime.UtcNow,
            NextAttemptUtc = DateTime.UtcNow
        });

        var service = CreateService(context, queueStore, jobService, transientRetryCount: 0);

        var firstLease = await service.DequeueAsync(CancellationToken.None);
        Assert.That(firstLease, Is.Not.Null);
        await service.MarkCompletedAsync(firstLease!.QueueItemId, CancellationToken.None);

        var secondLease = await service.DequeueAsync(CancellationToken.None);
        Assert.That(secondLease, Is.Not.Null);
        await service.MarkFailedAsync(secondLease!.QueueItemId, "timeout", CancellationToken.None);

        var job = await jobStore.GetAsync("job_partial");

        Assert.Multiple(() =>
        {
            Assert.That(job, Is.Not.Null);
            Assert.That(job!.ProcessedTargets, Is.EqualTo(2));
            Assert.That(job.SuccessCount, Is.EqualTo(1));
            Assert.That(job.FailedCount, Is.EqualTo(1));
            Assert.That(job.Status, Is.EqualTo(CrawlJobStatuses.CompletedWithFailures));
            Assert.That(job.PerCategoryBreakdown.Single().ProcessedTargets, Is.EqualTo(2));
            Assert.That(job.PerCategoryBreakdown.Single().FailedCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task CancelAsync_CancelsQueuedTargetsAndFinalizesJobWhenActiveWorkCompletes()
    {
        var context = MongoIntegrationTestFixture.Context;
        var queueStore = new CrawlQueueRepository(context);
        var jobStore = new CrawlJobRepository(context);
        var jobService = new CrawlJobService(jobStore, new EmptyKnownCrawlTargetStore(), new NoOpSourceDiscoveryService(), queueStore, new PermissiveCrawlGovernanceService(), new NullManagementAuditService());
        await jobStore.UpsertAsync(CreateJob("job_cancel", totalTargets: 2));
        await queueStore.UpsertAsync(new CrawlQueueItem
        {
            Id = "queue-active",
            JobId = "job_cancel",
            SourceName = "currys",
            SourceUrl = "https://currys.example/tv-1",
            CategoryKey = "tv",
            Status = "queued",
            EnqueuedUtc = DateTime.UtcNow.AddMinutes(-1),
            NextAttemptUtc = DateTime.UtcNow.AddMinutes(-1)
        });
        await queueStore.UpsertAsync(new CrawlQueueItem
        {
            Id = "queue-cancelled",
            JobId = "job_cancel",
            SourceName = "ao",
            SourceUrl = "https://ao.example/tv-2",
            CategoryKey = "tv",
            Status = "queued",
            EnqueuedUtc = DateTime.UtcNow,
            NextAttemptUtc = DateTime.UtcNow
        });

        var service = CreateService(context, queueStore, jobService, transientRetryCount: 2);

        var activeLease = await service.DequeueAsync(CancellationToken.None);
        Assert.That(activeLease, Is.Not.Null);

        var cancelRequestedJob = await jobService.CancelAsync("job_cancel", CancellationToken.None);
        var cancelledQueueItem = await queueStore.GetByIdAsync("queue-cancelled");

        Assert.Multiple(() =>
        {
            Assert.That(cancelRequestedJob, Is.Not.Null);
            Assert.That(cancelRequestedJob!.Status, Is.EqualTo(CrawlJobStatuses.CancelRequested));
            Assert.That(cancelRequestedJob.CancelledCount, Is.EqualTo(1));
            Assert.That(cancelRequestedJob.ProcessedTargets, Is.EqualTo(1));
            Assert.That(cancelledQueueItem, Is.Not.Null);
            Assert.That(cancelledQueueItem!.Status, Is.EqualTo("cancelled"));
        });

        await service.MarkCompletedAsync(activeLease!.QueueItemId, CancellationToken.None);

        var finalJob = await jobStore.GetAsync("job_cancel");

        Assert.Multiple(() =>
        {
            Assert.That(finalJob, Is.Not.Null);
            Assert.That(finalJob!.ProcessedTargets, Is.EqualTo(2));
            Assert.That(finalJob.SuccessCount, Is.EqualTo(1));
            Assert.That(finalJob.CancelledCount, Is.EqualTo(1));
            Assert.That(finalJob.Status, Is.EqualTo(CrawlJobStatuses.Cancelled));
        });
    }

    [Test]
    public async Task MultipleCategories_AreTrackedIndependentlyWithinOneJob()
    {
        var context = MongoIntegrationTestFixture.Context;
        var queueStore = new CrawlQueueRepository(context);
        var jobStore = new CrawlJobRepository(context);
        var jobService = new CrawlJobService(jobStore, new EmptyKnownCrawlTargetStore(), new NoOpSourceDiscoveryService(), queueStore, new PermissiveCrawlGovernanceService(), new NullManagementAuditService());
        await jobStore.UpsertAsync(new CrawlJob
        {
            JobId = "job_multi",
            RequestType = CrawlJobRequestTypes.Category,
            RequestedCategories = ["monitor", "tv"],
            TotalTargets = 2,
            StartedAt = DateTime.UtcNow.AddMinutes(-1),
            LastUpdatedAt = DateTime.UtcNow.AddMinutes(-1),
            Status = CrawlJobStatuses.Pending,
            PerCategoryBreakdown =
            [
                new CrawlJobCategoryBreakdown { CategoryKey = "monitor", TotalTargets = 1 },
                new CrawlJobCategoryBreakdown { CategoryKey = "tv", TotalTargets = 1 }
            ]
        });
        await queueStore.UpsertAsync(new CrawlQueueItem
        {
            Id = "queue-monitor",
            JobId = "job_multi",
            SourceName = "ao",
            SourceUrl = "https://ao.example/monitor-1",
            CategoryKey = "monitor",
            Status = "queued",
            EnqueuedUtc = DateTime.UtcNow.AddMinutes(-1),
            NextAttemptUtc = DateTime.UtcNow.AddMinutes(-1)
        });
        await queueStore.UpsertAsync(new CrawlQueueItem
        {
            Id = "queue-tv",
            JobId = "job_multi",
            SourceName = "currys",
            SourceUrl = "https://currys.example/tv-1",
            CategoryKey = "tv",
            Status = "queued",
            EnqueuedUtc = DateTime.UtcNow,
            NextAttemptUtc = DateTime.UtcNow
        });

        var service = CreateService(context, queueStore, jobService);

        var firstLease = await service.DequeueAsync(CancellationToken.None);
        Assert.That(firstLease, Is.Not.Null);
        await service.MarkCompletedAsync(firstLease!.QueueItemId, CancellationToken.None);

        var secondLease = await service.DequeueAsync(CancellationToken.None);
        Assert.That(secondLease, Is.Not.Null);
        await service.MarkCompletedAsync(secondLease!.QueueItemId, CancellationToken.None);

        var job = await jobStore.GetAsync("job_multi");

        Assert.Multiple(() =>
        {
            Assert.That(job, Is.Not.Null);
            Assert.That(job!.Status, Is.EqualTo(CrawlJobStatuses.Completed));
            Assert.That(job.ProcessedTargets, Is.EqualTo(2));
            Assert.That(job.SuccessCount, Is.EqualTo(2));
            Assert.That(job.PerCategoryBreakdown.Single(item => item.CategoryKey == "monitor").SuccessCount, Is.EqualTo(1));
            Assert.That(job.PerCategoryBreakdown.Single(item => item.CategoryKey == "tv").SuccessCount, Is.EqualTo(1));
            Assert.That(job.PerCategoryBreakdown.All(item => item.ProcessedTargets == 1), Is.True);
        });
    }

    private static CrawlQueueService CreateService(MongoDbContext context, CrawlQueueRepository queueStore, CrawlJobService jobService, int transientRetryCount = 2)
    {
        return new CrawlQueueService(
            queueStore,
            new FakePriorityService(queueStore),
            new FakeBackoffService(),
            jobService,
            context,
            Options.Create(new CrawlPipelineOptions
            {
                TransientRetryCount = transientRetryCount,
                WorkerIdleDelayMilliseconds = 1
            }));
    }

    private static CrawlJob CreateJob(string jobId, int totalTargets)
    {
        return new CrawlJob
        {
            JobId = jobId,
            RequestType = CrawlJobRequestTypes.Category,
            RequestedCategories = ["tv"],
            TotalTargets = totalTargets,
            StartedAt = DateTime.UtcNow.AddMinutes(-1),
            LastUpdatedAt = DateTime.UtcNow.AddMinutes(-1),
            Status = CrawlJobStatuses.Pending,
            PerCategoryBreakdown =
            [
                new CrawlJobCategoryBreakdown
                {
                    CategoryKey = "tv",
                    TotalTargets = totalTargets
                }
            ]
        };
    }

    private sealed class EmptyKnownCrawlTargetStore : IKnownCrawlTargetStore
    {
        public Task<IReadOnlyList<CrawlJobTargetDescriptor>> ListKnownTargetsAsync(IReadOnlyCollection<string> categoryKeys, IReadOnlyCollection<string> sourceNames, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CrawlJobTargetDescriptor>>([]);

        public Task<IReadOnlyList<CrawlJobTargetDescriptor>> ListTargetsForProductsAsync(IReadOnlyCollection<string> productIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CrawlJobTargetDescriptor>>([]);
    }

    private sealed class FakePriorityService(CrawlQueueRepository queueStore) : ICrawlPriorityService
    {
        public async Task<IReadOnlyList<CrawlPriorityAssessment>> GetPrioritiesAsync(DateTime utcNow, CancellationToken cancellationToken)
        {
            var queueItem = await queueStore.GetNextQueuedAsync(utcNow, cancellationToken);
            return queueItem is null
                ? []
                : [new CrawlPriorityAssessment { QueueItem = queueItem }];
        }
    }

    private sealed class FakeBackoffService : ICrawlBackoffService
    {
        public DateTime ComputeNextAttempt(CrawlContext context, SourceQualitySnapshot? sourceHistory, PageVolatilityProfile volatility)
            => context.UtcNow;

        public AdaptiveCrawlPolicy BuildPolicy(CrawlContext context, SourceQualitySnapshot? sourceHistory, PageVolatilityProfile volatility)
            => new();
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

    private sealed class NoOpSourceDiscoveryService : ISourceDiscoveryService
    {
        public Task<SourceDiscoveryPreview> PreviewAsync(IReadOnlyCollection<string>? categoryKeys, IReadOnlyCollection<string>? sourceIds, CancellationToken cancellationToken = default)
            => Task.FromResult(new SourceDiscoveryPreview());

        public Task<SourceDiscoverySeedResult> EnsureSeededAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new SourceDiscoverySeedResult());

        public Task<SourceDiscoverySeedResult> SeedAsync(IReadOnlyCollection<string>? categoryKeys, IReadOnlyCollection<string>? sourceIds, string? jobId, CancellationToken cancellationToken = default)
            => Task.FromResult(new SourceDiscoverySeedResult());
    }

    private sealed class NullManagementAuditService : IManagementAuditService
    {
        public Task RecordAsync(string action, string targetType, string targetId, IReadOnlyDictionary<string, string>? details = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ManagementAuditEntry>> ListRecentAsync(int take = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ManagementAuditEntry>>([]);
    }
}