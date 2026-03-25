using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using ProductNormaliser.AdminApi.Contracts;
using ProductNormaliser.Application.Crawls;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Mongo;

namespace ProductNormaliser.AdminApi.Controllers;

[ApiController]
[Route("api/crawl/jobs")]
public sealed class CrawlJobsController(
    ICrawlJobService crawlJobService,
    MongoDbContext mongoDbContext) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(CrawlJobListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetJobs([FromQuery] string? status, [FromQuery] string? requestType, [FromQuery] string? category, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            var jobs = await crawlJobService.ListAsync(new CrawlJobQuery
            {
                Status = status,
                RequestType = requestType,
                CategoryKey = category,
                Page = page,
                PageSize = pageSize
            }, cancellationToken);

            var metricsByJobId = await BuildJobMetricsAsync(jobs.Items, cancellationToken);

            return Ok(new CrawlJobListResponse
            {
                Items = jobs.Items.Select(job => Map(job, metricsByJobId)).ToArray(),
                Page = jobs.Page,
                PageSize = jobs.PageSize,
                TotalCount = jobs.TotalCount,
                TotalPages = jobs.TotalPages
            });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [string.IsNullOrWhiteSpace(exception.ParamName) ? "query" : exception.ParamName] = [exception.Message]
            }));
        }
    }

    [HttpGet("{jobId}")]
    [ProducesResponseType(typeof(CrawlJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJob(string jobId, CancellationToken cancellationToken)
    {
        var job = await crawlJobService.GetAsync(jobId, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        var metricsByJobId = await BuildJobMetricsAsync([job], cancellationToken);
        return Ok(Map(job, metricsByJobId));
    }

    [HttpPost("{jobId}/cancel")]
    [ProducesResponseType(typeof(CrawlJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelJob(string jobId, CancellationToken cancellationToken)
    {
        var job = await crawlJobService.CancelAsync(jobId, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        var metricsByJobId = await BuildJobMetricsAsync([job], cancellationToken);
        return Ok(Map(job, metricsByJobId));
    }

    [HttpPost]
    [ProducesResponseType(typeof(CrawlJobDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateJob([FromBody] Contracts.CreateCrawlJobRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var job = await crawlJobService.CreateAsync(new Application.Crawls.CreateCrawlJobRequest
            {
                RequestType = request.RequestType,
                RequestedCategories = request.RequestedCategories,
                RequestedSources = request.RequestedSources,
                RequestedProductIds = request.RequestedProductIds
            }, cancellationToken);

            var metricsByJobId = await BuildJobMetricsAsync([job], cancellationToken);

            return CreatedAtAction(nameof(GetJob), new { jobId = job.JobId }, Map(job, metricsByJobId));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [string.IsNullOrWhiteSpace(exception.ParamName) ? "request" : exception.ParamName] = [exception.Message]
            }));
        }
    }

    private CrawlJobDto Map(CrawlJob job, IReadOnlyDictionary<string, CrawlJobMetrics> metricsByJobId)
    {
        metricsByJobId.TryGetValue(job.JobId, out var metrics);
        metrics ??= CrawlJobMetrics.Empty;

        return new CrawlJobDto
        {
            JobId = job.JobId,
            RequestType = job.RequestType,
            RequestedCategories = job.RequestedCategories,
            RequestedSources = job.RequestedSources,
            RequestedProductIds = job.RequestedProductIds,
            TotalTargets = job.TotalTargets,
            ProcessedTargets = job.ProcessedTargets,
            SuccessCount = job.SuccessCount,
            SkippedCount = job.SkippedCount,
            FailedCount = job.FailedCount,
            CancelledCount = job.CancelledCount,
            DiscoveredUrlCount = job.DiscoveredUrlCount,
            ConfirmedProductTargetCount = job.ConfirmedProductCount,
            PromotedProductTargetCount = metrics.PromotedProductTargetCount,
            PromotedProductProcessedCount = metrics.PromotedProductProcessedCount,
            ProductYieldingTargetCount = metrics.ProductYieldingTargetCount,
            ProductNoExtractionCount = metrics.ProductNoExtractionCount,
            ExtractedProductCount = metrics.ExtractedProductCount,
            RejectedPageCount = job.RejectedPageCount,
            BlockedPageCount = job.BlockedPageCount,
            DiscoveryQueueDepth = metrics.DiscoveryQueueDepth,
            ActiveSourceCoverage = metrics.ActiveSourceCoverage,
            SourceCoveragePercent = metrics.SourceCoveragePercent,
            DiscoveryCompletionPercent = CalculatePercent(job.ProcessedTargets, job.TotalTargets),
            CrawledProductUrlCount = metrics.CrawledProductUrlCount,
            ProductQueueDepth = metrics.ProductQueueDepth,
            ProductFailureCount = metrics.ProductFailureCount,
            StartedAt = job.StartedAt,
            LastUpdatedAt = job.LastUpdatedAt,
            EstimatedCompletion = job.EstimatedCompletion,
            Status = job.Status,
            PerCategoryBreakdown = job.PerCategoryBreakdown.Select(item =>
            {
                metrics.CategoryMetrics.TryGetValue(item.CategoryKey, out var categoryMetrics);
                categoryMetrics ??= new CrawlJobCategoryMetrics(0, 0, 0m, 0, 0, 0, 0, 0, 0, 0, 0);

                return new CrawlJobCategoryBreakdownDto
                {
                    CategoryKey = item.CategoryKey,
                    TotalTargets = item.TotalTargets,
                    ProcessedTargets = item.ProcessedTargets,
                    SuccessCount = item.SuccessCount,
                    SkippedCount = item.SkippedCount,
                    FailedCount = item.FailedCount,
                    CancelledCount = item.CancelledCount,
                    DiscoveredUrlCount = item.DiscoveredUrlCount,
                    ConfirmedProductTargetCount = item.ConfirmedProductCount,
                    PromotedProductTargetCount = categoryMetrics.PromotedProductTargetCount,
                    PromotedProductProcessedCount = categoryMetrics.PromotedProductProcessedCount,
                    ProductYieldingTargetCount = categoryMetrics.ProductYieldingTargetCount,
                    ProductNoExtractionCount = categoryMetrics.ProductNoExtractionCount,
                    ExtractedProductCount = categoryMetrics.ExtractedProductCount,
                    RejectedPageCount = item.RejectedPageCount,
                    BlockedPageCount = item.BlockedPageCount,
                    DiscoveryQueueDepth = categoryMetrics.DiscoveryQueueDepth,
                    ActiveSourceCoverage = categoryMetrics.ActiveSourceCoverage,
                    SourceCoveragePercent = categoryMetrics.SourceCoveragePercent,
                    DiscoveryCompletionPercent = CalculatePercent(item.ProcessedTargets, item.TotalTargets),
                    CrawledProductUrlCount = categoryMetrics.CrawledProductUrlCount,
                    ProductQueueDepth = categoryMetrics.ProductQueueDepth,
                    ProductFailureCount = categoryMetrics.ProductFailureCount
                };
            }).ToArray()
        };
    }

    private async Task<IReadOnlyDictionary<string, CrawlJobMetrics>> BuildJobMetricsAsync(IReadOnlyList<CrawlJob> jobs, CancellationToken cancellationToken)
    {
        if (jobs.Count == 0)
        {
            return new Dictionary<string, CrawlJobMetrics>(StringComparer.OrdinalIgnoreCase);
        }

        var jobIds = jobs.Select(job => job.JobId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var discoveredUrls = await mongoDbContext.DiscoveredUrls
            .Find(item => item.JobId != null && jobIds.Contains(item.JobId))
            .ToListAsync(cancellationToken);
        var discoveryQueueItems = await mongoDbContext.DiscoveryQueueItems
            .Find(item => item.JobId != null && jobIds.Contains(item.JobId))
            .ToListAsync(cancellationToken);
        var promotedQueueItems = await mongoDbContext.CrawlQueueItems
            .Find(item => item.InitiatingJobId != null && jobIds.Contains(item.InitiatingJobId))
            .ToListAsync(cancellationToken);
        var crawlSources = await mongoDbContext.CrawlSources
            .Find(Builders<CrawlSource>.Filter.Empty)
            .ToListAsync(cancellationToken);

        var relevantSourceNames = promotedQueueItems
            .Select(item => item.SourceName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var relevantSourceUrls = promotedQueueItems
            .Select(item => item.SourceUrl)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var crawlLogs = relevantSourceNames.Length == 0 || relevantSourceUrls.Length == 0
            ? []
            : await mongoDbContext.CrawlLogs
                .Find(item => relevantSourceNames.Contains(item.SourceName) && relevantSourceUrls.Contains(item.Url))
                .ToListAsync(cancellationToken);
        var sourceProducts = relevantSourceNames.Length == 0 || relevantSourceUrls.Length == 0
            ? []
            : await mongoDbContext.SourceProducts
                .Find(item => relevantSourceNames.Contains(item.SourceName) && relevantSourceUrls.Contains(item.SourceUrl))
                .ToListAsync(cancellationToken);

        var discoveredByJob = discoveredUrls.GroupBy(item => item.JobId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var discoveryQueueByJob = discoveryQueueItems.GroupBy(item => item.JobId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var promotedTargetsByJob = promotedQueueItems.GroupBy(item => item.InitiatingJobId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var crawlLogsByTarget = crawlLogs.GroupBy(item => BuildSourceUrlKey(item.SourceName, item.Url), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var sourceProductsByTarget = sourceProducts.GroupBy(item => BuildSourceUrlCategoryKey(item.SourceName, item.SourceUrl, item.CategoryKey), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        return jobs.ToDictionary(
            job => job.JobId,
            job => BuildJobMetrics(job, crawlSources, discoveredByJob, discoveryQueueByJob, promotedTargetsByJob, crawlLogsByTarget, sourceProductsByTarget),
            StringComparer.OrdinalIgnoreCase);
    }

    private static CrawlJobMetrics BuildJobMetrics(
        CrawlJob job,
        IReadOnlyList<CrawlSource> crawlSources,
        IReadOnlyDictionary<string, DiscoveredUrl[]> discoveredByJob,
        IReadOnlyDictionary<string, DiscoveryQueueItem[]> discoveryQueueByJob,
        IReadOnlyDictionary<string, CrawlQueueItem[]> promotedTargetsByJob,
        IReadOnlyDictionary<string, CrawlLog[]> crawlLogsByTarget,
        IReadOnlyDictionary<string, SourceProduct[]> sourceProductsByTarget)
    {
        discoveredByJob.TryGetValue(job.JobId, out var discoveredUrls);
        discoveryQueueByJob.TryGetValue(job.JobId, out var discoveryQueueItems);
        promotedTargetsByJob.TryGetValue(job.JobId, out var promotedTargets);
        discoveredUrls ??= [];
        discoveryQueueItems ??= [];
        promotedTargets ??= [];

        var overallActiveSourceCoverage = discoveryQueueItems.Select(item => item.SourceId)
            .Concat(discoveredUrls.Select(item => item.SourceId))
            .Where(sourceId => !string.IsNullOrWhiteSpace(sourceId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var relevantCategories = GetRelevantCategories(job).ToArray();
        var eligibleSourceCount = crawlSources.Count(source => IsEligibleSourceForJob(source, job, relevantCategories));
        var promotedTargetSummaries = promotedTargets
            .Select(item => BuildPromotedTargetSummary(item, crawlLogsByTarget, sourceProductsByTarget))
            .ToArray();

        var categoryMetrics = job.PerCategoryBreakdown.ToDictionary(
            item => item.CategoryKey,
            item => BuildCategoryMetrics(job, item, crawlSources, discoveredUrls, discoveryQueueItems, promotedTargets, crawlLogsByTarget, sourceProductsByTarget),
            StringComparer.OrdinalIgnoreCase);

        return new CrawlJobMetrics(
            DiscoveryQueueDepth: discoveryQueueItems.Count(IsDiscoveryQueuedOrProcessing),
            ActiveSourceCoverage: overallActiveSourceCoverage,
            SourceCoveragePercent: CalculatePercent(overallActiveSourceCoverage, eligibleSourceCount),
            CrawledProductUrlCount: promotedTargetSummaries.Count(item => item.Processed),
            ProductQueueDepth: Math.Max(0, promotedTargetSummaries.Length - promotedTargetSummaries.Count(item => item.Processed)),
            ProductFailureCount: promotedTargetSummaries.Count(item => item.Failed),
            PromotedProductTargetCount: promotedTargetSummaries.Length,
            PromotedProductProcessedCount: promotedTargetSummaries.Count(item => item.Processed),
            ProductYieldingTargetCount: promotedTargetSummaries.Count(item => item.YieldedProducts),
            ProductNoExtractionCount: promotedTargetSummaries.Count(item => item.NoProductsExtracted),
            ExtractedProductCount: promotedTargetSummaries.Sum(item => item.ExtractedProductCount),
            CategoryMetrics: categoryMetrics);
    }

    private static CrawlJobCategoryMetrics BuildCategoryMetrics(
        CrawlJob job,
        CrawlJobCategoryBreakdown breakdown,
        IReadOnlyList<CrawlSource> crawlSources,
        IReadOnlyList<DiscoveredUrl> discoveredUrls,
        IReadOnlyList<DiscoveryQueueItem> discoveryQueueItems,
        IReadOnlyList<CrawlQueueItem> promotedTargets,
        IReadOnlyDictionary<string, CrawlLog[]> crawlLogsByTarget,
        IReadOnlyDictionary<string, SourceProduct[]> sourceProductsByTarget)
    {
        var categoryDiscoveredUrls = discoveredUrls.Where(item => string.Equals(item.CategoryKey, breakdown.CategoryKey, StringComparison.OrdinalIgnoreCase)).ToArray();
        var categoryDiscoveryQueueItems = discoveryQueueItems.Where(item => string.Equals(item.CategoryKey, breakdown.CategoryKey, StringComparison.OrdinalIgnoreCase)).ToArray();
        var categoryPromotedTargets = promotedTargets.Where(item => string.Equals(item.CategoryKey, breakdown.CategoryKey, StringComparison.OrdinalIgnoreCase)).ToArray();
        var promotedTargetSummaries = categoryPromotedTargets
            .Select(item => BuildPromotedTargetSummary(item, crawlLogsByTarget, sourceProductsByTarget))
            .ToArray();
        var activeSourceCoverage = categoryDiscoveryQueueItems.Select(item => item.SourceId)
            .Concat(categoryDiscoveredUrls.Select(item => item.SourceId))
            .Where(sourceId => !string.IsNullOrWhiteSpace(sourceId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var eligibleSourceCount = crawlSources.Count(source => IsEligibleSourceForJob(source, job, [breakdown.CategoryKey]));

        return new CrawlJobCategoryMetrics(
            DiscoveryQueueDepth: categoryDiscoveryQueueItems.Count(IsDiscoveryQueuedOrProcessing),
            ActiveSourceCoverage: activeSourceCoverage,
            SourceCoveragePercent: CalculatePercent(activeSourceCoverage, eligibleSourceCount),
            CrawledProductUrlCount: promotedTargetSummaries.Count(item => item.Processed),
            ProductQueueDepth: Math.Max(0, promotedTargetSummaries.Length - promotedTargetSummaries.Count(item => item.Processed)),
            ProductFailureCount: promotedTargetSummaries.Count(item => item.Failed),
            PromotedProductTargetCount: promotedTargetSummaries.Length,
            PromotedProductProcessedCount: promotedTargetSummaries.Count(item => item.Processed),
            ProductYieldingTargetCount: promotedTargetSummaries.Count(item => item.YieldedProducts),
            ProductNoExtractionCount: promotedTargetSummaries.Count(item => item.NoProductsExtracted),
            ExtractedProductCount: promotedTargetSummaries.Sum(item => item.ExtractedProductCount));
    }

    private static IEnumerable<string> GetRelevantCategories(CrawlJob job)
    {
        return job.RequestedCategories
            .Concat(job.PerCategoryBreakdown.Select(item => item.CategoryKey))
            .Where(categoryKey => !string.IsNullOrWhiteSpace(categoryKey))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsEligibleSourceForJob(CrawlSource source, CrawlJob job, IReadOnlyCollection<string> relevantCategories)
    {
        var matchesRequestedSource = job.RequestedSources.Count == 0
            || job.RequestedSources.Any(requestedSource => string.Equals(requestedSource, source.Id, StringComparison.OrdinalIgnoreCase));
        if (!matchesRequestedSource)
        {
            return false;
        }

        return relevantCategories.Count == 0
            || source.SupportedCategoryKeys.Any(categoryKey => relevantCategories.Contains(categoryKey, StringComparer.OrdinalIgnoreCase));
    }

    private static PromotedTargetSummary BuildPromotedTargetSummary(
        CrawlQueueItem promotedTarget,
        IReadOnlyDictionary<string, CrawlLog[]> crawlLogsByTarget,
        IReadOnlyDictionary<string, SourceProduct[]> sourceProductsByTarget)
    {
        crawlLogsByTarget.TryGetValue(BuildSourceUrlKey(promotedTarget.SourceName, promotedTarget.SourceUrl), out var crawlLogs);
        sourceProductsByTarget.TryGetValue(BuildSourceUrlCategoryKey(promotedTarget.SourceName, promotedTarget.SourceUrl, promotedTarget.CategoryKey), out var sourceProducts);

        var relevantLogs = (crawlLogs ?? [])
            .Where(item => item.TimestampUtc >= promotedTarget.EnqueuedUtc)
            .OrderBy(item => item.TimestampUtc)
            .ToArray();
        var relevantProducts = (sourceProducts ?? [])
            .Where(item => item.FetchedUtc >= promotedTarget.EnqueuedUtc)
            .ToArray();
        var latestLog = relevantLogs.LastOrDefault();
        var yieldedProducts = relevantProducts.Length > 0 || relevantLogs.Any(item => string.Equals(item.ExtractionOutcome, "products_extracted", StringComparison.OrdinalIgnoreCase) && item.ExtractedProductCount > 0);
        var processed = relevantLogs.Length > 0 || relevantProducts.Length > 0;
        var noProductsExtracted = !yieldedProducts
            && relevantLogs.Any(item => string.Equals(item.ExtractionOutcome, "no_products", StringComparison.OrdinalIgnoreCase));
        var failed = processed
            && !yieldedProducts
            && latestLog is not null
            && string.Equals(latestLog.Status, "failed", StringComparison.OrdinalIgnoreCase);

        return new PromotedTargetSummary(
            promotedTarget.CategoryKey,
            processed,
            yieldedProducts,
            noProductsExtracted,
            failed,
            relevantProducts.Length);
    }

    private static string BuildSourceUrlKey(string sourceName, string url)
    {
        if (string.IsNullOrWhiteSpace(sourceName) || string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        return $"{sourceName.Trim().ToLowerInvariant()}::{url.Trim().ToLowerInvariant()}";
    }

    private static string BuildSourceUrlCategoryKey(string sourceName, string url, string categoryKey)
    {
        if (string.IsNullOrWhiteSpace(sourceName) || string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(categoryKey))
        {
            return string.Empty;
        }

        return $"{sourceName.Trim().ToLowerInvariant()}::{categoryKey.Trim().ToLowerInvariant()}::{url.Trim().ToLowerInvariant()}";
    }

    private static bool IsDiscoveryQueuedOrProcessing(DiscoveryQueueItem queueItem)
    {
        return string.Equals(queueItem.State, "queued", StringComparison.OrdinalIgnoreCase)
            || string.Equals(queueItem.State, "processing", StringComparison.OrdinalIgnoreCase);
    }

    private static decimal CalculatePercent(int numerator, int denominator)
    {
        if (denominator == 0)
        {
            return 0m;
        }

        return decimal.Round((decimal)numerator / denominator * 100m, 2, MidpointRounding.AwayFromZero);
    }

    private sealed record CrawlJobMetrics(
        int DiscoveryQueueDepth,
        int ActiveSourceCoverage,
        decimal SourceCoveragePercent,
        int CrawledProductUrlCount,
        int ProductQueueDepth,
        int ProductFailureCount,
        int PromotedProductTargetCount,
        int PromotedProductProcessedCount,
        int ProductYieldingTargetCount,
        int ProductNoExtractionCount,
        int ExtractedProductCount,
        IReadOnlyDictionary<string, CrawlJobCategoryMetrics> CategoryMetrics)
    {
        public static CrawlJobMetrics Empty { get; } = new(0, 0, 0m, 0, 0, 0, 0, 0, 0, 0, 0, new Dictionary<string, CrawlJobCategoryMetrics>(StringComparer.OrdinalIgnoreCase));
    }

    private sealed record CrawlJobCategoryMetrics(
        int DiscoveryQueueDepth,
        int ActiveSourceCoverage,
        decimal SourceCoveragePercent,
        int CrawledProductUrlCount,
        int ProductQueueDepth,
        int ProductFailureCount,
        int PromotedProductTargetCount,
        int PromotedProductProcessedCount,
        int ProductYieldingTargetCount,
        int ProductNoExtractionCount,
        int ExtractedProductCount);

    private sealed record PromotedTargetSummary(
        string CategoryKey,
        bool Processed,
        bool YieldedProducts,
        bool NoProductsExtracted,
        bool Failed,
        int ExtractedProductCount);
}