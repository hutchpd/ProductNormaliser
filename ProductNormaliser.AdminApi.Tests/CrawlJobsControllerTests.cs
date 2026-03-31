using Microsoft.AspNetCore.Mvc;
using Mongo2Go;
using MongoDB.Driver;
using ProductNormaliser.AdminApi.Contracts;
using ProductNormaliser.AdminApi.Controllers;
using ProductNormaliser.Application.Crawls;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Mongo;

namespace ProductNormaliser.AdminApi.Tests;

public sealed class CrawlJobsControllerTests
{
    [Test]
    public async Task GetJobs_ReturnsMetrics_ForCategoryCrawlWithoutMongoLinqFailure()
    {
        using var runner = MongoDbRunner.Start(singleNodeReplSet: true);
        var client = new MongoClient(runner.ConnectionString);
        var context = new MongoDbContext(client, $"crawl_jobs_controller_tests_{Guid.NewGuid():N}");
        await context.EnsureIndexesAsync();

        var enqueuedUtc = DateTime.UtcNow.AddMinutes(-5);
        var job = new CrawlJob
        {
            JobId = "job_tv_1",
            RequestType = CrawlJobRequestTypes.Category,
            RequestedCategories = ["tv"],
            TotalTargets = 4,
            ProcessedTargets = 1,
            SuccessCount = 1,
            DiscoveredUrlCount = 1,
            ConfirmedProductCount = 1,
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            LastUpdatedAt = DateTime.UtcNow,
            Status = CrawlJobStatuses.Running,
            PerCategoryBreakdown =
            [
                new CrawlJobCategoryBreakdown
                {
                    CategoryKey = "tv",
                    TotalTargets = 4,
                    ProcessedTargets = 1,
                    SuccessCount = 1,
                    DiscoveredUrlCount = 1,
                    ConfirmedProductCount = 1
                }
            ]
        };

        await context.CrawlSources.InsertOneAsync(new CrawlSource
        {
            Id = "ao_uk",
            DisplayName = "AO UK",
            BaseUrl = "https://ao.example/",
            Host = "ao.example",
            IsEnabled = true,
            SupportedCategoryKeys = ["tv"],
            ThrottlingPolicy = new SourceThrottlingPolicy(),
            CreatedUtc = DateTime.UtcNow.AddDays(-1),
            UpdatedUtc = DateTime.UtcNow
        });
        await context.DiscoveredUrls.InsertOneAsync(new DiscoveredUrl
        {
            Id = "discovered_1",
            JobId = job.JobId,
            SourceId = "ao_uk",
            CategoryKey = "tv",
            Url = "https://ao.example/tv-1",
            NormalizedUrl = "https://ao.example/tv-1",
            Classification = "product",
            State = "queued",
            Depth = 1,
            AttemptCount = 0,
            FirstSeenUtc = enqueuedUtc,
            LastSeenUtc = enqueuedUtc
        });
        await context.DiscoveryQueueItems.InsertOneAsync(new DiscoveryQueueItem
        {
            Id = "queue_1",
            JobId = job.JobId,
            SourceId = "ao_uk",
            CategoryKey = "tv",
            Url = "https://ao.example/tv-2",
            NormalizedUrl = "https://ao.example/tv-2",
            Classification = "product",
            State = "queued",
            Depth = 1,
            AttemptCount = 0,
            EnqueuedUtc = enqueuedUtc
        });
        await context.CrawlQueueItems.InsertOneAsync(new CrawlQueueItem
        {
            Id = "crawl_target_1",
            JobId = "crawl_queue_job_1",
            InitiatingJobId = job.JobId,
            SourceName = "AO UK",
            SourceUrl = "https://ao.example/tv-1",
            CategoryKey = "tv",
            Status = "completed",
            AttemptCount = 1,
            EnqueuedUtc = enqueuedUtc
        });
        await context.CrawlLogs.InsertOneAsync(new CrawlLog
        {
            Id = "crawl_log_1",
            SourceName = "AO UK",
            Url = "https://ao.example/tv-1",
            Status = "succeeded",
            ExtractionOutcome = "products_extracted",
            DurationMs = 250,
            ExtractedProductCount = 2,
            TimestampUtc = enqueuedUtc.AddSeconds(30)
        });
        await context.SourceProducts.InsertOneAsync(new SourceProduct
        {
            Id = "source_product_1",
            SourceName = "AO UK",
            SourceUrl = "https://ao.example/tv-1",
            CategoryKey = "tv",
            RawSchemaJson = "{}",
            FetchedUtc = enqueuedUtc.AddSeconds(45)
        });

        var controller = new CrawlJobsController(new FakeCrawlJobService(job), context);

        var result = await controller.GetJobs(status: null, requestType: null, category: null, page: 1, pageSize: 20, cancellationToken: CancellationToken.None);

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var payload = ok!.Value as CrawlJobListResponse;
        Assert.That(payload, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(payload!.Items, Has.Count.EqualTo(1));
            Assert.That(payload.Items[0].JobId, Is.EqualTo(job.JobId));
            Assert.That(payload.Items[0].DiscoveryQueueDepth, Is.EqualTo(1));
            Assert.That(payload.Items[0].PromotedProductTargetCount, Is.EqualTo(1));
            Assert.That(payload.Items[0].PromotedProductProcessedCount, Is.EqualTo(1));
            Assert.That(payload.Items[0].ProductYieldingTargetCount, Is.EqualTo(1));
            Assert.That(payload.Items[0].ExtractedProductCount, Is.EqualTo(1));
            Assert.That(payload.Items[0].ActiveSourceCoverage, Is.EqualTo(1));
            Assert.That(payload.Items[0].PerCategoryBreakdown, Has.Count.EqualTo(1));
            Assert.That(payload.Items[0].PerCategoryBreakdown[0].PromotedProductTargetCount, Is.EqualTo(1));
        });

        await context.Client.DropDatabaseAsync(context.Database.DatabaseNamespace.DatabaseName);
    }

    private sealed class FakeCrawlJobService(CrawlJob job) : ICrawlJobService
    {
        public Task<CrawlJobPage> ListAsync(CrawlJobQuery? query = null, CancellationToken cancellationToken = default)
        {
            _ = query;
            _ = cancellationToken;
            return Task.FromResult(new CrawlJobPage
            {
                Items = [job],
                Page = 1,
                PageSize = 20,
                TotalCount = 1
            });
        }

        public Task<CrawlJob?> GetAsync(string jobId, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Equals(jobId, job.JobId, StringComparison.OrdinalIgnoreCase) ? job : null);

        public Task<CrawlJob> CreateAsync(ProductNormaliser.Application.Crawls.CreateCrawlJobRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<CrawlJob?> CancelAsync(string jobId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task MarkStartedAsync(string jobId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RecordTargetOutcomeAsync(string jobId, string categoryKey, string outcome, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}