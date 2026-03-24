using System.Diagnostics;
using System.Globalization;
using ProductNormaliser.Application.Discovery;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProductNormaliser.Application.Governance;
using ProductNormaliser.Application.Observability;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Crawls;

public sealed class CrawlJobService(
    ICrawlJobStore crawlJobStore,
    IKnownCrawlTargetStore knownCrawlTargetStore,
    ISourceDiscoveryService sourceDiscoveryService,
    ICrawlJobQueueWriter crawlJobQueueWriter,
    ICrawlGovernanceService crawlGovernanceService,
    IManagementAuditService managementAuditService,
    ILogger<CrawlJobService>? logger = null) : ICrawlJobService
{
    private readonly ILogger<CrawlJobService> logger = logger ?? NullLogger<CrawlJobService>.Instance;

    public Task<CrawlJobPage> ListAsync(CrawlJobQuery? query = null, CancellationToken cancellationToken = default)
    {
        return crawlJobStore.ListAsync(NormalizeQuery(query), cancellationToken);
    }

    public Task<CrawlJob?> GetAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return crawlJobStore.GetAsync(NormalizeRequiredValue(jobId, nameof(jobId)), cancellationToken);
    }

    public async Task<CrawlJob> CreateAsync(CreateCrawlJobRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = ProductNormaliserTelemetry.ActivitySource.StartActivity("crawl.job.create", ActivityKind.Internal);

        var requestType = NormalizeRequestType(request.RequestType);
        var categories = NormalizeValues(request.RequestedCategories);
        var sources = NormalizeValues(request.RequestedSources);
        var productIds = NormalizeValues(request.RequestedProductIds);

        ValidateRequest(requestType, categories, sources, productIds);

        if (string.Equals(requestType, CrawlJobRequestTypes.Category, StringComparison.OrdinalIgnoreCase))
        {
            return await CreateDiscoverySeededCategoryJobAsync(requestType, categories, sources, productIds, request, activity, cancellationToken);
        }

        var targets = await ResolveTargetsAsync(requestType, categories, sources, productIds, cancellationToken);
        if (targets.Count == 0)
        {
            throw new ArgumentException("No crawlable targets were found for the requested job.", nameof(request));
        }

        crawlGovernanceService.ValidateCrawlRequest(requestType, categories, sources, productIds, targets, nameof(request));

        var now = DateTime.UtcNow;
        var jobId = $"job_{Guid.NewGuid():N}";
        var breakdown = targets
            .GroupBy(target => target.CategoryKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => new CrawlJobCategoryBreakdown
            {
                CategoryKey = group.Key,
                TotalTargets = group.Count()
            })
            .OrderBy(group => group.CategoryKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var job = new CrawlJob
        {
            JobId = jobId,
            RequestType = requestType,
            RequestedCategories = categories,
            RequestedSources = sources,
            RequestedProductIds = productIds,
            TotalTargets = targets.Count,
            StartedAt = now,
            LastUpdatedAt = now,
            Status = CrawlJobStatuses.Pending,
            PerCategoryBreakdown = breakdown
        };

        activity?.SetTag("crawl.job.id", job.JobId);
        activity?.SetTag("crawl.job.request_type", requestType);
        activity?.SetTag("crawl.job.target_count", targets.Count);

        await crawlJobStore.UpsertAsync(job, cancellationToken);

        for (var index = 0; index < targets.Count; index++)
        {
            var target = targets[index];
            await crawlJobQueueWriter.UpsertAsync(new CrawlQueueItem
            {
                Id = $"{jobId}:{index + 1}",
                JobId = jobId,
                SourceName = target.SourceName,
                SourceUrl = target.SourceUrl,
                CategoryKey = target.CategoryKey,
                Status = "queued",
                AttemptCount = 0,
                ConsecutiveFailureCount = 0,
                ImportanceScore = 1.0m,
                EnqueuedUtc = now,
                LastAttemptUtc = null,
                NextAttemptUtc = now,
                LastError = null
            }, cancellationToken);
        }

        await managementAuditService.RecordAsync(
            ManagementAuditActions.CrawlJobCreated,
            "crawl_job",
            job.JobId,
            new Dictionary<string, string>
            {
                ["requestType"] = requestType,
                ["targetCount"] = targets.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["requestedCategories"] = string.Join(",", categories),
                ["requestedSources"] = string.Join(",", sources),
                ["requestedProductIds"] = string.Join(",", productIds)
            },
            cancellationToken);

        logger.LogInformation(
            "Created crawl job {JobId} with request type {RequestType}; targets={TargetCount}, categories={RequestedCategories}, sources={RequestedSources}, products={RequestedProductIds}",
            job.JobId,
            requestType,
            targets.Count,
            string.Join(',', categories),
            string.Join(',', sources),
            string.Join(',', productIds));

        var createTags = new TagList
        {
            { "request.type", requestType }
        };
        ProductNormaliserTelemetry.CrawlJobsCreated.Add(1, createTags);
        ProductNormaliserTelemetry.CrawlJobTargetCount.Record(targets.Count, createTags);

        return job;
    }

    private async Task<CrawlJob> CreateDiscoverySeededCategoryJobAsync(
        string requestType,
        IReadOnlyList<string> categories,
        IReadOnlyList<string> sources,
        IReadOnlyList<string> productIds,
        CreateCrawlJobRequest request,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        var preview = await sourceDiscoveryService.PreviewAsync(categories, sources, cancellationToken);
        if (preview.Seeds.Count == 0)
        {
            throw new ArgumentException("No discovery seeds were found for the requested categories and sources.", nameof(request));
        }

        var governanceTargets = preview.Seeds
            .Select(seed => new CrawlJobTargetDescriptor
            {
                SourceName = seed.SourceId,
                SourceUrl = seed.Url,
                CategoryKey = seed.CategoryKey
            })
            .ToArray();

        crawlGovernanceService.ValidateCrawlRequest(requestType, categories, sources, productIds, governanceTargets, nameof(request));

        var now = DateTime.UtcNow;
        var jobId = $"job_{Guid.NewGuid():N}";
        var categoryBreakdown = preview.Seeds
            .Select(seed => seed.CategoryKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Union(categories, StringComparer.OrdinalIgnoreCase)
            .OrderBy(categoryKey => categoryKey, StringComparer.OrdinalIgnoreCase)
            .Select(categoryKey => new CrawlJobCategoryBreakdown
            {
                CategoryKey = categoryKey,
                TotalTargets = 0,
                DiscoveredUrlCount = 0,
                ConfirmedProductCount = 0,
                RejectedPageCount = 0,
                BlockedPageCount = 0
            })
            .ToList();

        var job = new CrawlJob
        {
            JobId = jobId,
            RequestType = requestType,
            RequestedCategories = categories,
            RequestedSources = sources,
            RequestedProductIds = productIds,
            TotalTargets = 0,
            DiscoveredUrlCount = 0,
            ConfirmedProductCount = 0,
            RejectedPageCount = 0,
            BlockedPageCount = 0,
            StartedAt = now,
            LastUpdatedAt = now,
            Status = CrawlJobStatuses.Pending,
            PerCategoryBreakdown = categoryBreakdown
        };

        activity?.SetTag("crawl.job.id", job.JobId);
        activity?.SetTag("crawl.job.request_type", requestType);
        activity?.SetTag("crawl.job.target_count", preview.Seeds.Count);

        await crawlJobStore.UpsertAsync(job, cancellationToken);
        var seedResult = await sourceDiscoveryService.SeedAsync(categories, sources, jobId, cancellationToken);
        var seededJob = await crawlJobStore.GetAsync(jobId, cancellationToken) ?? job;

        await managementAuditService.RecordAsync(
            ManagementAuditActions.CrawlJobCreated,
            "crawl_job",
            seededJob.JobId,
            new Dictionary<string, string>
            {
                ["requestType"] = requestType,
                ["targetCount"] = seededJob.TotalTargets.ToString(CultureInfo.InvariantCulture),
                ["requestedCategories"] = string.Join(",", categories),
                ["requestedSources"] = string.Join(",", sources),
                ["requestedProductIds"] = string.Join(",", productIds),
                ["seededSourceCount"] = seedResult.SourceCount.ToString(CultureInfo.InvariantCulture),
                ["seededCategoryCount"] = seedResult.CategoryCount.ToString(CultureInfo.InvariantCulture),
                ["seededItemCount"] = seedResult.SeedCount.ToString(CultureInfo.InvariantCulture)
            },
            cancellationToken);

        logger.LogInformation(
            "Created discovery-seeded crawl job {JobId} with initialSeeds={InitialSeedCount}, categories={RequestedCategories}, sources={RequestedSources}",
            seededJob.JobId,
            seededJob.TotalTargets,
            string.Join(',', categories),
            string.Join(',', sources));

        var createTags = new TagList
        {
            { "request.type", requestType }
        };
        ProductNormaliserTelemetry.CrawlJobsCreated.Add(1, createTags);
        ProductNormaliserTelemetry.CrawlJobTargetCount.Record(seededJob.TotalTargets, createTags);

        return seededJob;
    }

    public async Task<CrawlJob?> CancelAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var normalizedJobId = NormalizeRequiredValue(jobId, nameof(jobId));
        var job = await crawlJobStore.GetAsync(normalizedJobId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        if (IsTerminalStatus(job.Status))
        {
            return job;
        }

        var cancelledItems = await crawlJobQueueWriter.CancelQueuedItemsAsync(normalizedJobId, "Cancelled by operator.", cancellationToken);
        foreach (var cancelledItem in cancelledItems)
        {
            job.ProcessedTargets += 1;
            job.CancelledCount += 1;

            var breakdown = job.PerCategoryBreakdown.FirstOrDefault(item => string.Equals(item.CategoryKey, cancelledItem.CategoryKey, StringComparison.OrdinalIgnoreCase));
            if (breakdown is null)
            {
                breakdown = new CrawlJobCategoryBreakdown { CategoryKey = cancelledItem.CategoryKey };
                job.PerCategoryBreakdown.Add(breakdown);
            }

            breakdown.ProcessedTargets += 1;
            breakdown.CancelledCount += 1;
        }

        job.LastUpdatedAt = DateTime.UtcNow;
        job.EstimatedCompletion = null;
        job.Status = job.ProcessedTargets >= job.TotalTargets
            ? CrawlJobStatuses.Cancelled
            : CrawlJobStatuses.CancelRequested;

        await crawlJobStore.UpsertAsync(job, cancellationToken);

        logger.LogInformation(
            "Cancellation requested for crawl job {JobId}; cancelledTargets={CancelledCount}, processedTargets={ProcessedTargets}, totalTargets={TotalTargets}, status={Status}",
            job.JobId,
            job.CancelledCount,
            job.ProcessedTargets,
            job.TotalTargets,
            job.Status);

        if (string.Equals(job.Status, CrawlJobStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            ProductNormaliserTelemetry.CrawlJobsCompleted.Add(1, new TagList
            {
                { "status", job.Status }
            });
        }

        return job;
    }

    public async Task MarkStartedAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var job = await crawlJobStore.GetAsync(NormalizeRequiredValue(jobId, nameof(jobId)), cancellationToken);
        if (job is null)
        {
            return;
        }

        if (!string.Equals(job.Status, CrawlJobStatuses.Pending, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        job.Status = CrawlJobStatuses.Running;
        job.LastUpdatedAt = DateTime.UtcNow;
        await crawlJobStore.UpsertAsync(job, cancellationToken);

        logger.LogInformation(
            "Started crawl job {JobId}; requestType={RequestType}, processedTargets={ProcessedTargets}, totalTargets={TotalTargets}",
            job.JobId,
            job.RequestType,
            job.ProcessedTargets,
            job.TotalTargets);

        ProductNormaliserTelemetry.CrawlJobsStarted.Add(1, new TagList
        {
            { "request.type", job.RequestType }
        });
    }

    public async Task RecordTargetOutcomeAsync(string jobId, string categoryKey, string outcome, CancellationToken cancellationToken = default)
    {
        using var activity = ProductNormaliserTelemetry.ActivitySource.StartActivity("crawl.job.record_outcome", ActivityKind.Internal);
        var job = await crawlJobStore.GetAsync(NormalizeRequiredValue(jobId, nameof(jobId)), cancellationToken);
        if (job is null)
        {
            return;
        }

        if (job.ProcessedTargets >= job.TotalTargets)
        {
            if (!IsTerminalStatus(job.Status))
            {
                var completedAt = DateTime.UtcNow;
                job.LastUpdatedAt = completedAt;
                job.EstimatedCompletion = completedAt;
                job.Status = DetermineCompletedStatus(job);
                await crawlJobStore.UpsertAsync(job, cancellationToken);
            }

            return;
        }

        var normalizedCategoryKey = NormalizeRequiredValue(categoryKey, nameof(categoryKey));
        var normalizedOutcome = NormalizeOutcome(outcome);
        var now = DateTime.UtcNow;

        activity?.SetTag("crawl.job.id", job.JobId);
        activity?.SetTag("crawl.job.category", normalizedCategoryKey);
        activity?.SetTag("crawl.job.outcome", normalizedOutcome);

        var breakdown = job.PerCategoryBreakdown.FirstOrDefault(item => string.Equals(item.CategoryKey, normalizedCategoryKey, StringComparison.OrdinalIgnoreCase));
        if (breakdown is null)
        {
            breakdown = new CrawlJobCategoryBreakdown { CategoryKey = normalizedCategoryKey };
            job.PerCategoryBreakdown.Add(breakdown);
        }

        job.ProcessedTargets += 1;
        breakdown.ProcessedTargets += 1;

        switch (normalizedOutcome)
        {
            case "completed":
                job.SuccessCount += 1;
                breakdown.SuccessCount += 1;
                break;
            case "skipped":
                job.SkippedCount += 1;
                breakdown.SkippedCount += 1;
                break;
            case "cancelled":
                job.CancelledCount += 1;
                breakdown.CancelledCount += 1;
                break;
            default:
                job.FailedCount += 1;
                breakdown.FailedCount += 1;
                break;
        }

        job.LastUpdatedAt = now;
        if (string.Equals(job.Status, CrawlJobStatuses.Pending, StringComparison.OrdinalIgnoreCase))
        {
            job.Status = CrawlJobStatuses.Running;
        }

        if (job.ProcessedTargets > 0)
        {
            var elapsed = now - job.StartedAt;
            var averageTicksPerTarget = elapsed.Ticks / job.ProcessedTargets;
            var remainingTargets = Math.Max(0, job.TotalTargets - job.ProcessedTargets);
            job.EstimatedCompletion = remainingTargets == 0
                ? now
                : now.AddTicks(averageTicksPerTarget * remainingTargets);
        }

        if (job.ProcessedTargets >= job.TotalTargets)
        {
            job.EstimatedCompletion = now;
            job.Status = DetermineCompletedStatus(job);
        }

        await crawlJobStore.UpsertAsync(job, cancellationToken);

        logger.LogInformation(
            "Recorded crawl job outcome {Outcome} for job {JobId} in category {CategoryKey}; processedTargets={ProcessedTargets}/{TotalTargets}, status={Status}, success={SuccessCount}, skipped={SkippedCount}, failed={FailedCount}, cancelled={CancelledCount}",
            normalizedOutcome,
            job.JobId,
            normalizedCategoryKey,
            job.ProcessedTargets,
            job.TotalTargets,
            job.Status,
            job.SuccessCount,
            job.SkippedCount,
            job.FailedCount,
            job.CancelledCount);

        ProductNormaliserTelemetry.CrawlJobTargetsRecorded.Add(1, new TagList
        {
            { "category", normalizedCategoryKey },
            { "status", normalizedOutcome }
        });

        if (IsTerminalStatus(job.Status) && job.ProcessedTargets >= job.TotalTargets)
        {
            ProductNormaliserTelemetry.CrawlJobsCompleted.Add(1, new TagList
            {
                { "status", job.Status }
            });

            logger.LogInformation(
                "Crawl job {JobId} reached terminal status {Status}; totalTargets={TotalTargets}, success={SuccessCount}, skipped={SkippedCount}, failed={FailedCount}, cancelled={CancelledCount}, completionUtc={CompletionUtc}",
                job.JobId,
                job.Status,
                job.TotalTargets,
                job.SuccessCount,
                job.SkippedCount,
                job.FailedCount,
                job.CancelledCount,
                job.EstimatedCompletion?.ToString("u", CultureInfo.InvariantCulture));
        }
    }

    private async Task<IReadOnlyList<CrawlJobTargetDescriptor>> ResolveTargetsAsync(
        string requestType,
        IReadOnlyList<string> categories,
        IReadOnlyList<string> sources,
        IReadOnlyList<string> productIds,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CrawlJobTargetDescriptor> rawTargets = requestType switch
        {
            CrawlJobRequestTypes.Category => await knownCrawlTargetStore.ListKnownTargetsAsync(categories, sources, cancellationToken),
            CrawlJobRequestTypes.Source => await knownCrawlTargetStore.ListKnownTargetsAsync(categories, sources, cancellationToken),
            CrawlJobRequestTypes.ProductSelection => await knownCrawlTargetStore.ListTargetsForProductsAsync(productIds, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(requestType), requestType, "Unsupported crawl job request type.")
        };

        return rawTargets
            .GroupBy(
                target => $"{target.SourceName}|{target.SourceUrl}|{target.CategoryKey}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(target => target.CategoryKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(target => target.SourceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(target => target.SourceUrl, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string DetermineCompletedStatus(CrawlJob job)
    {
        if (string.Equals(job.Status, CrawlJobStatuses.CancelRequested, StringComparison.OrdinalIgnoreCase)
            || job.CancelledCount > 0)
        {
            return CrawlJobStatuses.Cancelled;
        }

        return job.FailedCount switch
        {
            0 => CrawlJobStatuses.Completed,
            _ when job.FailedCount >= job.TotalTargets => CrawlJobStatuses.Failed,
            _ => CrawlJobStatuses.CompletedWithFailures
        };
    }

    private static CrawlJobQuery NormalizeQuery(CrawlJobQuery? query)
    {
        var page = Math.Max(1, query?.Page ?? 1);
        var pageSize = Math.Clamp(query?.PageSize ?? 20, 1, 100);

        return new CrawlJobQuery
        {
            Status = NormalizeOptionalStatus(query?.Status),
            RequestType = NormalizeOptionalRequestType(query?.RequestType),
            CategoryKey = NormalizeOptionalValue(query?.CategoryKey),
            Page = page,
            PageSize = pageSize
        };
    }

    private static void ValidateRequest(
        string requestType,
        IReadOnlyCollection<string> categories,
        IReadOnlyCollection<string> sources,
        IReadOnlyCollection<string> productIds)
    {
        switch (requestType)
        {
            case CrawlJobRequestTypes.Category when categories.Count == 0:
                throw new ArgumentException("At least one category is required for a category crawl job.", nameof(categories));
            case CrawlJobRequestTypes.Source when sources.Count == 0:
                throw new ArgumentException("At least one source is required for a source crawl job.", nameof(sources));
            case CrawlJobRequestTypes.ProductSelection when productIds.Count == 0:
                throw new ArgumentException("At least one product identifier is required for a product recrawl job.", nameof(productIds));
        }
    }

    private static string NormalizeRequestType(string requestType)
    {
        var normalized = NormalizeRequiredValue(requestType, nameof(requestType)).Replace('-', '_');
        return normalized switch
        {
            CrawlJobRequestTypes.Category => normalized,
            CrawlJobRequestTypes.Source => normalized,
            CrawlJobRequestTypes.ProductSelection => normalized,
            _ => throw new ArgumentException($"Unsupported crawl job request type '{requestType}'.", nameof(requestType))
        };
    }

    private static string? NormalizeOptionalRequestType(string? requestType)
    {
        return string.IsNullOrWhiteSpace(requestType)
            ? null
            : NormalizeRequestType(requestType);
    }

    private static string? NormalizeOptionalStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        var normalized = NormalizeRequiredValue(status, nameof(status)).Replace('-', '_');
        return normalized switch
        {
            CrawlJobStatuses.Pending => normalized,
            CrawlJobStatuses.Running => normalized,
            CrawlJobStatuses.CancelRequested => normalized,
            CrawlJobStatuses.Cancelled => normalized,
            CrawlJobStatuses.Completed => normalized,
            CrawlJobStatuses.CompletedWithFailures => normalized,
            CrawlJobStatuses.Failed => normalized,
            _ => throw new ArgumentException($"Unsupported crawl job status '{status}'.", nameof(status))
        };
    }

    private static string NormalizeOutcome(string outcome)
    {
        var normalized = NormalizeRequiredValue(outcome, nameof(outcome));
        return normalized switch
        {
            "completed" => normalized,
            "skipped" => normalized,
            "cancelled" => normalized,
            "failed" => normalized,
            _ => throw new ArgumentException($"Unsupported crawl job outcome '{outcome}'.", nameof(outcome))
        };
    }

    private static bool IsTerminalStatus(string status)
    {
        return string.Equals(status, CrawlJobStatuses.Completed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, CrawlJobStatuses.CompletedWithFailures, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, CrawlJobStatuses.Failed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, CrawlJobStatuses.Cancelled, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> NormalizeValues(IReadOnlyCollection<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeRequiredValue(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();
    }
}