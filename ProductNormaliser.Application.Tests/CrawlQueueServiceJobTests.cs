using ProductNormaliser.Application.Crawls;
using ProductNormaliser.Application.Governance;
using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Crawling;
using ProductNormaliser.Infrastructure.Mongo;
using ProductNormaliser.Infrastructure.Mongo.Repositories;

namespace ProductNormaliser.Tests;

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
        var jobService = new CrawlJobService(jobStore, new EmptyKnownCrawlTargetStore(), queueStore, new PermissiveCrawlGovernanceService(), new NullManagementAuditService());
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

        var service = new CrawlQueueService(queueStore, new FakePriorityService(queueStore), new FakeBackoffService(), jobService, context);

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
        var jobService = new CrawlJobService(jobStore, new EmptyKnownCrawlTargetStore(), queueStore, new PermissiveCrawlGovernanceService(), new NullManagementAuditService());
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

        var service = new CrawlQueueService(queueStore, new FakePriorityService(queueStore), new FakeBackoffService(), jobService, context);

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
            => DateTime.UtcNow.AddMinutes(15);

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

    private sealed class NullManagementAuditService : IManagementAuditService
    {
        public Task RecordAsync(string action, string targetType, string targetId, IReadOnlyDictionary<string, string>? details = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ManagementAuditEntry>> ListRecentAsync(int take = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ManagementAuditEntry>>([]);
    }
}