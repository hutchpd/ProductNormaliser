using MongoDB.Driver;
using ProductNormaliser.AdminApi.Contracts;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Normalisation;
using ProductNormaliser.Core.Schemas;
using ProductNormaliser.Infrastructure.Mongo;
using ProductNormaliser.Infrastructure.Mongo.Repositories;

namespace ProductNormaliser.AdminApi.Services;

public sealed class AdminQueryService(
    ICrawlLogStore crawlLogStore,
    ICanonicalProductStore canonicalProductStore,
    ISourceProductStore sourceProductStore,
    IProductChangeEventStore productChangeEventStore,
    MongoDbContext mongoDbContext,
    ICategorySchemaRegistry? categorySchemaRegistry = null,
    ICategoryAttributeNormaliserRegistry? categoryAttributeNormaliserRegistry = null) : IAdminQueryService
{
    private readonly ICategorySchemaRegistry categorySchemaRegistry = categorySchemaRegistry ?? DefaultCategoryRegistries.CreateSchemaRegistry();
    private readonly ICategoryAttributeNormaliserRegistry categoryAttributeNormaliserRegistry = categoryAttributeNormaliserRegistry ?? DefaultCategoryRegistries.CreateAttributeNormaliserRegistry();

    public async Task<IReadOnlyList<CrawlLogDto>> GetCrawlLogsAsync(CancellationToken cancellationToken)
    {
        var logs = await crawlLogStore.ListAsync(cancellationToken: cancellationToken);
        return logs.Select(Map).ToArray();
    }

    public async Task<CrawlLogDto?> GetCrawlLogAsync(string id, CancellationToken cancellationToken)
    {
        var log = await crawlLogStore.GetByIdAsync(id, cancellationToken);
        return log is null ? null : Map(log);
    }

    public async Task<IReadOnlyList<QueueItemDto>> GetQueueAsync(CancellationToken cancellationToken)
    {
        var cursor = await mongoDbContext.CrawlQueueItems.FindAsync(Builders<CrawlQueueItem>.Filter.Empty, cancellationToken: cancellationToken);
        var queueItems = await cursor.ToListAsync(cancellationToken);
        return queueItems.OrderBy(item => item.EnqueuedUtc).Select(item => new QueueItemDto
        {
            Id = item.Id,
            SourceName = item.SourceName,
            SourceUrl = item.SourceUrl,
            CategoryKey = item.CategoryKey,
            Status = item.Status,
            AttemptCount = item.AttemptCount,
            ConsecutiveFailureCount = item.ConsecutiveFailureCount,
            ImportanceScore = item.ImportanceScore,
            EnqueuedUtc = item.EnqueuedUtc,
            LastAttemptUtc = item.LastAttemptUtc,
            NextAttemptUtc = item.NextAttemptUtc,
            LastError = item.LastError
        }).ToArray();
    }

    public async Task<ProductListResponse> ListProductsAsync(string? categoryKey, string? search, int? minSourceCount, string? freshness, string? conflictStatus, string? completenessStatus, string? sort, int page, int pageSize, CancellationToken cancellationToken)
    {
        var normalizedCategoryKey = string.IsNullOrWhiteSpace(categoryKey) ? null : categoryKey.Trim();
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var effectivePage = Math.Max(1, page);
        var effectivePageSize = Math.Clamp(pageSize, 1, 100);

        var filter = Builders<CanonicalProduct>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(normalizedCategoryKey))
        {
            filter &= Builders<CanonicalProduct>.Filter.Eq(product => product.CategoryKey, normalizedCategoryKey);
        }

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var regex = new MongoDB.Bson.BsonRegularExpression(normalizedSearch, "i");
            filter &= Builders<CanonicalProduct>.Filter.Or(
                Builders<CanonicalProduct>.Filter.Eq(product => product.Id, normalizedSearch),
                Builders<CanonicalProduct>.Filter.Regex(product => product.DisplayName, regex),
                Builders<CanonicalProduct>.Filter.Regex(product => product.Brand, regex),
                Builders<CanonicalProduct>.Filter.Regex(product => product.ModelNumber, regex),
                Builders<CanonicalProduct>.Filter.Regex(product => product.Gtin, regex));
        }

        var products = await mongoDbContext.CanonicalProducts.Find(filter)
            .SortByDescending(product => product.UpdatedUtc)
            .ToListAsync(cancellationToken);

        var summaries = products
            .Select(product => new
            {
                Product = product,
                Analysis = ProductAnalysisProjection.BuildSummary(product, categorySchemaRegistry, categoryAttributeNormaliserRegistry)
            })
            .Where(entry => ProductAnalysisProjection.MatchesFilters(entry.Analysis, minSourceCount, freshness, conflictStatus, completenessStatus))
            .Select(entry => (entry.Product, entry.Analysis))
            .ToArray();

        var sortedItems = ProductAnalysisProjection.ApplySort(summaries, sort);

        var totalCount = sortedItems.Count;
        var pagedItems = sortedItems
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToArray();

        return new ProductListResponse
        {
            Items = pagedItems.Select(entry => new ProductSummaryResponse
            {
                Id = entry.Product.Id,
                CategoryKey = entry.Product.CategoryKey,
                Brand = entry.Product.Brand,
                ModelNumber = entry.Product.ModelNumber,
                Gtin = entry.Product.Gtin,
                DisplayName = entry.Product.DisplayName,
                SourceCount = entry.Analysis.SourceCount,
                AttributeCount = entry.Product.Attributes.Count,
                EvidenceCount = entry.Analysis.EvidenceCount,
                ConflictAttributeCount = entry.Analysis.ConflictAttributeCount,
                HasConflict = entry.Analysis.HasConflict,
                CompletenessScore = entry.Analysis.CompletenessScore,
                CompletenessStatus = entry.Analysis.CompletenessStatus,
                PopulatedKeyAttributeCount = entry.Analysis.PopulatedKeyAttributeCount,
                ExpectedKeyAttributeCount = entry.Analysis.ExpectedKeyAttributeCount,
                FreshnessStatus = entry.Analysis.FreshnessStatus,
                FreshnessAgeDays = entry.Analysis.FreshnessAgeDays,
                KeyAttributes = entry.Analysis.KeyAttributes,
                UpdatedUtc = entry.Product.UpdatedUtc
            }).ToArray(),
            Page = effectivePage,
            PageSize = effectivePageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / effectivePageSize)
        };
    }

    public async Task<ProductDetailResponse?> GetProductAsync(string id, CancellationToken cancellationToken)
    {
        var canonicalProduct = await canonicalProductStore.GetByIdAsync(id, cancellationToken);
        if (canonicalProduct is null)
        {
            return null;
        }

        var sourceProducts = new List<SourceProduct>();
        foreach (var sourceLink in canonicalProduct.Sources)
        {
            var sourceProduct = await sourceProductStore.GetByIdAsync(sourceLink.SourceProductId, cancellationToken)
                ?? await sourceProductStore.GetBySourceAsync(sourceLink.SourceName, sourceLink.SourceUrl, cancellationToken);

            if (sourceProduct is not null)
            {
                sourceProducts.Add(sourceProduct);
            }
        }

        var analysis = ProductAnalysisProjection.BuildSummary(canonicalProduct, categorySchemaRegistry, categoryAttributeNormaliserRegistry);

        return new ProductDetailResponse
        {
            Id = canonicalProduct.Id,
            CategoryKey = canonicalProduct.CategoryKey,
            Brand = canonicalProduct.Brand,
            ModelNumber = canonicalProduct.ModelNumber,
            Gtin = canonicalProduct.Gtin,
            DisplayName = canonicalProduct.DisplayName,
            CreatedUtc = canonicalProduct.CreatedUtc,
            UpdatedUtc = canonicalProduct.UpdatedUtc,
            SourceCount = analysis.SourceCount,
            EvidenceCount = analysis.EvidenceCount,
            ConflictAttributeCount = analysis.ConflictAttributeCount,
            HasConflict = analysis.HasConflict,
            CompletenessScore = analysis.CompletenessScore,
            CompletenessStatus = analysis.CompletenessStatus,
            PopulatedKeyAttributeCount = analysis.PopulatedKeyAttributeCount,
            ExpectedKeyAttributeCount = analysis.ExpectedKeyAttributeCount,
            FreshnessStatus = analysis.FreshnessStatus,
            FreshnessAgeDays = analysis.FreshnessAgeDays,
            KeyAttributes = analysis.KeyAttributes,
            Attributes = canonicalProduct.Attributes.Values.Select(attribute => new ProductAttributeDetailDto
            {
                AttributeKey = attribute.AttributeKey,
                Value = attribute.Value,
                ValueType = attribute.ValueType,
                Unit = attribute.Unit,
                Confidence = attribute.Confidence,
                HasConflict = attribute.HasConflict,
                Evidence = attribute.Evidence.Select(evidence => new AttributeEvidenceDto
                {
                    SourceName = evidence.SourceName,
                    SourceUrl = evidence.SourceUrl,
                    SourceProductId = evidence.SourceProductId,
                    SourceAttributeKey = evidence.SourceAttributeKey,
                    RawValue = evidence.RawValue,
                    SelectorOrPath = evidence.SelectorOrPath,
                    Confidence = evidence.Confidence,
                    ObservedUtc = evidence.ObservedUtc
                }).ToArray()
            }).OrderBy(attribute => attribute.AttributeKey).ToArray(),
            SourceProducts = sourceProducts.Select(sourceProduct => new SourceProductDetailDto
            {
                Id = sourceProduct.Id,
                SourceName = sourceProduct.SourceName,
                SourceUrl = sourceProduct.SourceUrl,
                Brand = sourceProduct.Brand,
                ModelNumber = sourceProduct.ModelNumber,
                Gtin = sourceProduct.Gtin,
                Title = sourceProduct.Title,
                RawSchemaJson = sourceProduct.RawSchemaJson,
                RawAttributes = sourceProduct.RawAttributes.Values.Select(attribute => new SourceAttributeValueDto
                {
                    AttributeKey = attribute.AttributeKey,
                    Value = attribute.Value,
                    ValueType = attribute.ValueType,
                    Unit = attribute.Unit,
                    SourcePath = attribute.SourcePath
                }).OrderBy(attribute => attribute.AttributeKey).ToArray()
            }).ToArray()
        };
    }

    public async Task<IReadOnlyList<ProductChangeEventDto>> GetProductHistoryAsync(string id, CancellationToken cancellationToken)
    {
        var history = await productChangeEventStore.GetByCanonicalProductIdAsync(id, cancellationToken: cancellationToken);
        return history.Select(changeEvent => new ProductChangeEventDto
        {
            CanonicalProductId = changeEvent.CanonicalProductId,
            CategoryKey = changeEvent.CategoryKey,
            AttributeKey = changeEvent.AttributeKey,
            OldValue = changeEvent.OldValue,
            NewValue = changeEvent.NewValue,
            SourceName = changeEvent.SourceName,
            TimestampUtc = changeEvent.TimestampUtc
        }).ToArray();
    }

    public async Task<IReadOnlyList<ConflictDto>> GetConflictsAsync(CancellationToken cancellationToken)
    {
        var cursor = await mongoDbContext.MergeConflicts.FindAsync(Builders<MergeConflict>.Filter.Empty, cancellationToken: cancellationToken);
        var conflicts = await cursor.ToListAsync(cancellationToken);
        return conflicts.OrderByDescending(conflict => conflict.CreatedUtc).Select(conflict => new ConflictDto
        {
            Id = conflict.Id,
            CanonicalProductId = conflict.CanonicalProductId,
            AttributeKey = conflict.AttributeKey,
            ExistingValue = conflict.ExistingValue,
            IncomingValue = conflict.IncomingValue,
            Reason = conflict.Reason,
            Severity = conflict.Severity,
            Status = conflict.Status,
            SuggestedValue = conflict.SuggestedValue,
            SuggestedSourceName = conflict.SuggestedSourceName,
            SuggestedConfidence = conflict.SuggestedConfidence,
            HighestConfidenceValue = conflict.HighestConfidenceValue,
            CreatedUtc = conflict.CreatedUtc,
            ResolvedUtc = conflict.ResolvedUtc
        }).ToArray();
    }

    public async Task<StatsResponse> GetStatsAsync(CancellationToken cancellationToken)
    {
        var throughputWindowStartUtc = DateTime.UtcNow.AddHours(-24);
        var canonicalCursor = await mongoDbContext.CanonicalProducts.FindAsync(Builders<CanonicalProduct>.Filter.Empty, cancellationToken: cancellationToken);
        var canonicalProducts = await canonicalCursor.ToListAsync(cancellationToken);

        var sourceCursor = await mongoDbContext.SourceProducts.FindAsync(Builders<SourceProduct>.Filter.Empty, cancellationToken: cancellationToken);
        var sourceProducts = await sourceCursor.ToListAsync(cancellationToken);

        var crawlQueueItemsTask = mongoDbContext.CrawlQueueItems.Find(Builders<CrawlQueueItem>.Filter.Empty).ToListAsync(cancellationToken);
        var crawlJobsTask = mongoDbContext.CrawlJobs.Find(Builders<CrawlJob>.Filter.Empty).ToListAsync(cancellationToken);
        var recentLogsTask = mongoDbContext.CrawlLogs
            .Find(log => log.TimestampUtc >= throughputWindowStartUtc)
            .ToListAsync(cancellationToken);
        var sourceSnapshotsTask = mongoDbContext.SourceQualitySnapshots.Find(Builders<SourceQualitySnapshot>.Filter.Empty).ToListAsync(cancellationToken);
        var crawlSourcesTask = mongoDbContext.CrawlSources.Find(Builders<CrawlSource>.Filter.Empty).ToListAsync(cancellationToken);
        var discoveryQueueItemsTask = mongoDbContext.DiscoveryQueueItems.Find(Builders<DiscoveryQueueItem>.Filter.Empty).ToListAsync(cancellationToken);
        var discoveredUrlsTask = mongoDbContext.DiscoveredUrls.Find(Builders<DiscoveredUrl>.Filter.Empty).ToListAsync(cancellationToken);

        await Task.WhenAll(crawlQueueItemsTask, crawlJobsTask, recentLogsTask, sourceSnapshotsTask, crawlSourcesTask, discoveryQueueItemsTask, discoveredUrlsTask);

        var totalCanonicalProducts = canonicalProducts.Count;
        var totalSourceProducts = sourceProducts.Count;
        var averageAttributesPerProduct = totalCanonicalProducts == 0
            ? 0m
            : decimal.Round((decimal)canonicalProducts.Average(product => product.Attributes.Count), 2, MidpointRounding.AwayFromZero);
        var productsWithConflicts = canonicalProducts.Count(product => product.Attributes.Values.Any(attribute => attribute.HasConflict));
        var productsMissingKeyAttributes = canonicalProducts.Count(IsMissingKeyAttributes);

        var discoveryQueueItems = discoveryQueueItemsTask.Result;
        var discoveredUrls = discoveredUrlsTask.Result;
        var discoveryProcessedLast24Hours = discoveredUrls.Count(url => url.LastProcessedUtc is not null && url.LastProcessedUtc.Value >= throughputWindowStartUtc);
        var activeDiscoverySourceCount = discoveryQueueItems
            .Where(IsDiscoveryQueuedOrProcessing)
            .Select(item => item.SourceId)
            .Concat(discoveredUrls
                .Where(url => IsDiscoveryActiveWithinWindow(url, throughputWindowStartUtc))
                .Select(url => url.SourceId))
            .Where(sourceId => !string.IsNullOrWhiteSpace(sourceId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return new StatsResponse
        {
            TotalCanonicalProducts = totalCanonicalProducts,
            TotalSourceProducts = totalSourceProducts,
            AverageAttributesPerProduct = averageAttributesPerProduct,
            PercentProductsWithConflicts = ToPercent(productsWithConflicts, totalCanonicalProducts),
            PercentProductsMissingKeyAttributes = ToPercent(productsMissingKeyAttributes, totalCanonicalProducts),
            DiscoveryQueueDepth = discoveryQueueItems.Count(IsDiscoveryQueuedOrProcessing),
            DiscoveryProcessingRateLast24Hours = decimal.Round(discoveryProcessedLast24Hours / 24m, 2, MidpointRounding.AwayFromZero),
            DiscoveredUrlCountLast24Hours = discoveredUrls.Count(url => url.FirstSeenUtc >= throughputWindowStartUtc),
            ConfirmedProductUrlCountLast24Hours = discoveredUrls.Count(url => IsProductClassification(url.Classification)
                && url.PromotedToCrawlUtc is not null
                && url.PromotedToCrawlUtc.Value >= throughputWindowStartUtc),
            RejectedUrlCountLast24Hours = discoveredUrls.Count(url => string.Equals(url.State, "rejected", StringComparison.OrdinalIgnoreCase)
                && url.LastProcessedUtc is not null
                && url.LastProcessedUtc.Value >= throughputWindowStartUtc),
            RobotsBlockedCountLast24Hours = discoveredUrls.Count(url => string.Equals(url.State, "blocked", StringComparison.OrdinalIgnoreCase)
                && url.LastProcessedUtc is not null
                && url.LastProcessedUtc.Value >= throughputWindowStartUtc),
            ActiveDiscoverySourceCount = activeDiscoverySourceCount,
            Operational = BuildOperationalSummary(
                crawlQueueItemsTask.Result,
                crawlJobsTask.Result,
                recentLogsTask.Result,
                sourceSnapshotsTask.Result,
                crawlSourcesTask.Result,
                sourceProducts,
                discoveryQueueItems,
                discoveredUrls,
                throughputWindowStartUtc)
        };
    }

    private static OperationalSummaryDto BuildOperationalSummary(
        IReadOnlyList<CrawlQueueItem> queueItems,
        IReadOnlyList<CrawlJob> crawlJobs,
        IReadOnlyList<CrawlLog> recentLogs,
        IReadOnlyList<SourceQualitySnapshot> sourceSnapshots,
        IReadOnlyList<CrawlSource> crawlSources,
        IReadOnlyList<SourceProduct> sourceProducts,
        IReadOnlyList<DiscoveryQueueItem> discoveryQueueItems,
        IReadOnlyList<DiscoveredUrl> discoveredUrls,
        DateTime throughputWindowStartUtc)
    {
        var latestSnapshotsBySourceCategory = sourceSnapshots
            .GroupBy(snapshot => $"{snapshot.SourceName}|{snapshot.CategoryKey}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(snapshot => snapshot.TimestampUtc).First())
            .ToArray();

        var sourceMetrics = crawlSources
            .Select(source => BuildSourceOperationalMetric(source, queueItems, recentLogs, latestSnapshotsBySourceCategory, discoveryQueueItems, discoveredUrls, throughputWindowStartUtc))
            .OrderByDescending(metric => metric.FailureRateLast24Hours)
            .ThenByDescending(metric => metric.DiscoveryQueueDepth)
            .ThenByDescending(metric => metric.RetryQueueDepth)
            .ThenByDescending(metric => metric.QueueDepth)
            .ThenBy(metric => metric.SourceName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var categoryMetrics = crawlJobs.SelectMany(job => job.PerCategoryBreakdown.Select(item => item.CategoryKey))
            .Concat(queueItems.Select(item => item.CategoryKey))
            .Concat(latestSnapshotsBySourceCategory.Select(snapshot => snapshot.CategoryKey))
            .Concat(crawlSources.SelectMany(source => source.SupportedCategoryKeys))
            .Concat(discoveryQueueItems.Select(item => item.CategoryKey))
            .Concat(discoveredUrls.Select(item => item.CategoryKey))
            .Concat(sourceProducts.Select(product => product.CategoryKey))
            .Where(categoryKey => !string.IsNullOrWhiteSpace(categoryKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(categoryKey => BuildCategoryOperationalMetric(categoryKey, queueItems, crawlJobs, recentLogs, latestSnapshotsBySourceCategory, crawlSources, sourceProducts, discoveryQueueItems, discoveredUrls, throughputWindowStartUtc))
            .OrderByDescending(metric => metric.DiscoveryQueueDepth)
            .ThenByDescending(metric => metric.QueueDepth)
            .ThenByDescending(metric => metric.FailedCrawlsLast24Hours)
            .ThenBy(metric => metric.CategoryKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new OperationalSummaryDto
        {
            ActiveJobCount = crawlJobs.Count(job => IsActiveJobStatus(job.Status)),
            QueueDepth = queueItems.Count(IsQueuedOrProcessing),
            RetryQueueDepth = queueItems.Count(item => IsQueuedOrProcessing(item) && item.AttemptCount > 0),
            FailedQueueDepth = queueItems.Count(item => string.Equals(item.Status, "failed", StringComparison.OrdinalIgnoreCase)),
            ThroughputLast24Hours = recentLogs.Count,
            FailureCountLast24Hours = recentLogs.Count(log => string.Equals(log.Status, "failed", StringComparison.OrdinalIgnoreCase)),
            HealthySourceCount = sourceMetrics.Count(metric => string.Equals(metric.HealthStatus, "Healthy", StringComparison.OrdinalIgnoreCase)),
            AttentionSourceCount = sourceMetrics.Count(metric => string.Equals(metric.HealthStatus, "Attention", StringComparison.OrdinalIgnoreCase)
                || string.Equals(metric.HealthStatus, "Watch", StringComparison.OrdinalIgnoreCase)
                || string.Equals(metric.HealthStatus, "Degraded", StringComparison.OrdinalIgnoreCase)),
            Sources = sourceMetrics,
            Categories = categoryMetrics
        };
    }

    private static SourceOperationalMetricDto BuildSourceOperationalMetric(
        CrawlSource source,
        IReadOnlyList<CrawlQueueItem> queueItems,
        IReadOnlyList<CrawlLog> recentLogs,
        IReadOnlyList<SourceQualitySnapshot> latestSnapshotsBySourceCategory,
        IReadOnlyList<DiscoveryQueueItem> discoveryQueueItems,
        IReadOnlyList<DiscoveredUrl> discoveredUrls,
        DateTime throughputWindowStartUtc)
    {
        var sourceName = source.DisplayName;
        var sourceQueueItems = queueItems.Where(item => string.Equals(item.SourceName, sourceName, StringComparison.OrdinalIgnoreCase)).ToArray();
        var sourceLogs = recentLogs.Where(log => string.Equals(log.SourceName, sourceName, StringComparison.OrdinalIgnoreCase)).ToArray();
        var sourceSnapshots = latestSnapshotsBySourceCategory.Where(snapshot => string.Equals(snapshot.SourceName, sourceName, StringComparison.OrdinalIgnoreCase)).ToArray();
        var sourceDiscoveryQueueItems = discoveryQueueItems.Where(item => string.Equals(item.SourceId, source.Id, StringComparison.OrdinalIgnoreCase)).ToArray();
        var sourceDiscoveredUrls = discoveredUrls.Where(item => string.Equals(item.SourceId, source.Id, StringComparison.OrdinalIgnoreCase)).ToArray();
        var failedCrawls = sourceLogs.Count(log => string.Equals(log.Status, "failed", StringComparison.OrdinalIgnoreCase));
        var trustScore = sourceSnapshots.Length == 0 ? 0m : decimal.Round(sourceSnapshots.Average(snapshot => snapshot.HistoricalTrustScore), 2, MidpointRounding.AwayFromZero);
        var coveragePercent = sourceSnapshots.Length == 0 ? 0m : decimal.Round(sourceSnapshots.Average(snapshot => snapshot.AttributeCoverage), 2, MidpointRounding.AwayFromZero);
        var successfulCrawlRate = sourceSnapshots.Length == 0 ? 0m : decimal.Round(sourceSnapshots.Average(snapshot => snapshot.SuccessfulCrawlRate), 2, MidpointRounding.AwayFromZero);
        var discoveryCoverageByCategory = source.SupportedCategoryKeys
            .Concat(sourceDiscoveredUrls.Select(item => item.CategoryKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                categoryKey => categoryKey,
                categoryKey => CalculateDiscoveryCompletionPercent(sourceDiscoveredUrls.Where(item => string.Equals(item.CategoryKey, categoryKey, StringComparison.OrdinalIgnoreCase))),
                StringComparer.OrdinalIgnoreCase);

        return new SourceOperationalMetricDto
        {
            SourceName = sourceName,
            HealthStatus = sourceSnapshots.Length == 0 ? "Unknown" : DetermineHealthStatus(trustScore, successfulCrawlRate),
            QueueDepth = sourceQueueItems.Count(IsQueuedOrProcessing),
            DiscoveryQueueDepth = sourceDiscoveryQueueItems.Count(IsDiscoveryQueuedOrProcessing),
            RetryQueueDepth = sourceQueueItems.Count(item => IsQueuedOrProcessing(item) && item.AttemptCount > 0),
            FailedQueueDepth = sourceQueueItems.Count(item => string.Equals(item.Status, "failed", StringComparison.OrdinalIgnoreCase)),
            TotalCrawlsLast24Hours = sourceLogs.Length,
            FailedCrawlsLast24Hours = failedCrawls,
            FailureRateLast24Hours = ToPercent(failedCrawls, sourceLogs.Length),
            ListingPagesVisitedLast24Hours = sourceDiscoveredUrls.Count(item => string.Equals(item.Classification, "listing", StringComparison.OrdinalIgnoreCase)
                && item.LastProcessedUtc is not null
                && item.LastProcessedUtc.Value >= throughputWindowStartUtc),
            SitemapUrlsProcessedLast24Hours = sourceDiscoveredUrls.Count(item => string.Equals(item.Classification, "sitemap", StringComparison.OrdinalIgnoreCase)
                && item.LastProcessedUtc is not null
                && item.LastProcessedUtc.Value >= throughputWindowStartUtc),
            ConfirmedProductUrlsLast24Hours = sourceDiscoveredUrls.Count(item => IsProductClassification(item.Classification)
                && item.PromotedToCrawlUtc is not null
                && item.PromotedToCrawlUtc.Value >= throughputWindowStartUtc),
            DiscoveryCoverageByCategory = discoveryCoverageByCategory,
            TrustScore = trustScore,
            CoveragePercent = coveragePercent,
            SuccessfulCrawlRate = successfulCrawlRate,
            SnapshotUtc = sourceSnapshots.OrderByDescending(snapshot => snapshot.TimestampUtc).FirstOrDefault()?.TimestampUtc,
            LastCrawlUtc = sourceLogs.OrderByDescending(log => log.TimestampUtc).FirstOrDefault()?.TimestampUtc,
            LastDiscoveryUtc = sourceDiscoveredUrls
                .Select(GetDiscoveryActivityUtc)
                .Where(timestamp => timestamp is not null)
                .Max()
        };
    }

    private static CategoryOperationalMetricDto BuildCategoryOperationalMetric(
        string categoryKey,
        IReadOnlyList<CrawlQueueItem> queueItems,
        IReadOnlyList<CrawlJob> crawlJobs,
        IReadOnlyList<CrawlLog> recentLogs,
        IReadOnlyList<SourceQualitySnapshot> latestSnapshotsBySourceCategory,
        IReadOnlyList<CrawlSource> crawlSources,
        IReadOnlyList<SourceProduct> sourceProducts,
        IReadOnlyList<DiscoveryQueueItem> discoveryQueueItems,
        IReadOnlyList<DiscoveredUrl> discoveredUrls,
        DateTime throughputWindowStartUtc)
    {
        var categoryQueueItems = queueItems.Where(item => string.Equals(item.CategoryKey, categoryKey, StringComparison.OrdinalIgnoreCase)).ToArray();
        var supportedSourceNames = crawlSources
            .Where(source => source.SupportedCategoryKeys.Any(item => string.Equals(item, categoryKey, StringComparison.OrdinalIgnoreCase)))
            .Select(source => source.DisplayName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var categoryLogs = recentLogs.Where(log => supportedSourceNames.Contains(log.SourceName)).ToArray();
        var categoryDiscoveryQueueItems = discoveryQueueItems.Where(item => string.Equals(item.CategoryKey, categoryKey, StringComparison.OrdinalIgnoreCase)).ToArray();
        var categoryDiscoveredUrls = discoveredUrls.Where(item => string.Equals(item.CategoryKey, categoryKey, StringComparison.OrdinalIgnoreCase)).ToArray();
        var categorySourceProductsLast24Hours = sourceProducts.Count(product => string.Equals(product.CategoryKey, categoryKey, StringComparison.OrdinalIgnoreCase)
            && product.FetchedUtc >= throughputWindowStartUtc);
        var failedCrawls = categoryLogs.Count(log => string.Equals(log.Status, "failed", StringComparison.OrdinalIgnoreCase));
        var activeSourceCoverage = categoryDiscoveryQueueItems.Select(item => item.SourceId)
            .Concat(categoryDiscoveredUrls.Select(item => item.SourceId))
            .Where(sourceId => !string.IsNullOrWhiteSpace(sourceId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var eligibleSourceCount = crawlSources.Count(source => source.SupportedCategoryKeys.Any(item => string.Equals(item, categoryKey, StringComparison.OrdinalIgnoreCase)));

        return new CategoryOperationalMetricDto
        {
            CategoryKey = categoryKey,
            ActiveJobCount = crawlJobs.Count(job => IsActiveJobStatus(job.Status)
                && job.PerCategoryBreakdown.Any(item => string.Equals(item.CategoryKey, categoryKey, StringComparison.OrdinalIgnoreCase))),
            QueueDepth = categoryQueueItems.Count(IsQueuedOrProcessing),
            RetryQueueDepth = categoryQueueItems.Count(item => IsQueuedOrProcessing(item) && item.AttemptCount > 0),
            ThroughputLast24Hours = categoryLogs.Length,
            CrawledProductUrlCountLast24Hours = categorySourceProductsLast24Hours,
            FailedCrawlsLast24Hours = failedCrawls,
            FailureRateLast24Hours = ToPercent(failedCrawls, categoryLogs.Length),
            DistinctSourceCount = latestSnapshotsBySourceCategory
                .Where(snapshot => string.Equals(snapshot.CategoryKey, categoryKey, StringComparison.OrdinalIgnoreCase))
                .Select(snapshot => snapshot.SourceName)
                .Concat(categoryQueueItems.Select(item => item.SourceName))
                .Concat(categoryLogs.Select(log => log.SourceName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            DiscoveredUrlCount = categoryDiscoveredUrls.Length,
            ConfirmedProductTargetCount = categoryDiscoveredUrls.Count(item => IsProductClassification(item.Classification) && item.PromotedToCrawlUtc is not null),
            DiscoveryQueueDepth = categoryDiscoveryQueueItems.Count(IsDiscoveryQueuedOrProcessing),
            ActiveSourceCoverage = activeSourceCoverage,
            SourceCoveragePercent = ToPercent(activeSourceCoverage, eligibleSourceCount),
            DiscoveryCompletionPercent = CalculateDiscoveryCompletionPercent(categoryDiscoveredUrls)
        };
    }

    private static bool IsQueuedOrProcessing(CrawlQueueItem queueItem)
    {
        return string.Equals(queueItem.Status, "queued", StringComparison.OrdinalIgnoreCase)
            || string.Equals(queueItem.Status, "processing", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDiscoveryQueuedOrProcessing(DiscoveryQueueItem queueItem)
    {
        return string.Equals(queueItem.State, "queued", StringComparison.OrdinalIgnoreCase)
            || string.Equals(queueItem.State, "processing", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProductClassification(string classification)
    {
        return string.Equals(classification, "product", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDiscoveryActiveWithinWindow(DiscoveredUrl discoveredUrl, DateTime windowStartUtc)
    {
        return discoveredUrl.FirstSeenUtc >= windowStartUtc
            || discoveredUrl.LastSeenUtc >= windowStartUtc
            || (discoveredUrl.LastProcessedUtc is not null && discoveredUrl.LastProcessedUtc.Value >= windowStartUtc)
            || (discoveredUrl.PromotedToCrawlUtc is not null && discoveredUrl.PromotedToCrawlUtc.Value >= windowStartUtc);
    }

    private static DateTime? GetDiscoveryActivityUtc(DiscoveredUrl discoveredUrl)
    {
        var lastProcessedUtc = discoveredUrl.LastProcessedUtc;
        var promotedToCrawlUtc = discoveredUrl.PromotedToCrawlUtc;
        var lastSeenUtc = discoveredUrl.LastSeenUtc;

        return new[] { lastProcessedUtc, promotedToCrawlUtc, lastSeenUtc }
            .Where(timestamp => timestamp is not null)
            .Max();
    }

    private static decimal CalculateDiscoveryCompletionPercent(IEnumerable<DiscoveredUrl> discoveredUrls)
    {
        var materialized = discoveredUrls.ToArray();
        var totalCount = materialized.Length;
        if (totalCount == 0)
        {
            return 0m;
        }

        var processedCount = materialized.Count(item => item.LastProcessedUtc is not null || item.PromotedToCrawlUtc is not null);
        return ToPercent(processedCount, totalCount);
    }

    private static bool IsActiveJobStatus(string status)
    {
        return string.Equals(status, CrawlJobStatuses.Pending, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, CrawlJobStatuses.Running, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, CrawlJobStatuses.CancelRequested, StringComparison.OrdinalIgnoreCase);
    }

    private static string DetermineHealthStatus(decimal trustScore, decimal successfulCrawlRate)
    {
        if (trustScore >= 80m && successfulCrawlRate >= 90m)
        {
            return "Healthy";
        }

        if (trustScore >= 60m && successfulCrawlRate >= 75m)
        {
            return "Watch";
        }

        return "Attention";
    }

    private static CrawlLogDto Map(CrawlLog log)
    {
        return new CrawlLogDto
        {
            Id = log.Id,
            SourceName = log.SourceName,
            Url = log.Url,
            Status = log.Status,
            DurationMs = log.DurationMs,
            ContentHash = log.ContentHash,
            ExtractedProductCount = log.ExtractedProductCount,
            ErrorMessage = log.ErrorMessage,
            TimestampUtc = log.TimestampUtc
        };
    }

    private static decimal ToPercent(int numerator, int denominator)
    {
        if (denominator == 0)
        {
            return 0m;
        }

        return decimal.Round((decimal)numerator / denominator * 100m, 2, MidpointRounding.AwayFromZero);
    }

    private bool IsMissingKeyAttributes(CanonicalProduct product)
    {
        var requiredKeys = categorySchemaRegistry.GetSchema(product.CategoryKey)?.Attributes
            .Where(attribute => attribute.IsRequired)
            .Select(attribute => attribute.Key)
            ?? [];
        var keysToCheck = requiredKeys
            .Concat(categoryAttributeNormaliserRegistry.GetCompletenessAttributeKeys(product.CategoryKey))
            .Distinct(StringComparer.Ordinal);

        foreach (var key in keysToCheck)
        {
            if (key == "brand")
            {
                if (string.IsNullOrWhiteSpace(product.Brand))
                {
                    return true;
                }

                continue;
            }

            if (key == "model_number")
            {
                if (string.IsNullOrWhiteSpace(product.ModelNumber))
                {
                    return true;
                }

                continue;
            }

            if (!product.Attributes.TryGetValue(key, out var attributeValue) || attributeValue.Value is null)
            {
                return true;
            }
        }

        return false;
    }
}