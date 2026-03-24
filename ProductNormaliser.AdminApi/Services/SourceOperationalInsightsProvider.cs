using MongoDB.Driver;
using ProductNormaliser.AdminApi.Contracts;
using ProductNormaliser.Application.Categories;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Mongo;

namespace ProductNormaliser.AdminApi.Services;

public sealed class SourceOperationalInsightsProvider(
    MongoDbContext mongoDbContext,
    ICategoryMetadataService categoryMetadataService,
    ILogger<SourceOperationalInsightsProvider> logger) : ISourceOperationalInsightsProvider
{
    private static readonly TimeSpan InsightLoadTimeout = TimeSpan.FromSeconds(2);

    public async Task<IReadOnlyDictionary<string, SourceOperationalInsights>> BuildAsync(IReadOnlyList<CrawlSource> sources, CancellationToken cancellationToken)
    {
        if (sources.Count == 0)
        {
            return new Dictionary<string, SourceOperationalInsights>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(InsightLoadTimeout);
            var insightCancellationToken = timeoutCts.Token;

            var categoriesByKey = (await categoryMetadataService.ListAsync(enabledOnly: false, insightCancellationToken))
                .ToDictionary(category => category.CategoryKey, StringComparer.OrdinalIgnoreCase);

            var sourceDisplayNames = sources
                .Select(source => source.DisplayName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var snapshots = sourceDisplayNames.Length == 0
                ? []
                : await mongoDbContext.SourceQualitySnapshots
                    .Find(Builders<SourceQualitySnapshot>.Filter.In(snapshot => snapshot.SourceName, sourceDisplayNames))
                    .SortByDescending(snapshot => snapshot.TimestampUtc)
                    .Limit(5000)
                    .ToListAsync(insightCancellationToken);

            var crawlLogs = sourceDisplayNames.Length == 0
                ? []
                : await mongoDbContext.CrawlLogs
                    .Find(Builders<CrawlLog>.Filter.In(log => log.SourceName, sourceDisplayNames))
                    .SortByDescending(log => log.TimestampUtc)
                    .Limit(1000)
                    .ToListAsync(insightCancellationToken);

            var sourceIds = sources
                .Select(source => source.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var discoveryQueueItems = sourceIds.Length == 0
                ? []
                : await mongoDbContext.DiscoveryQueueItems
                    .Find(Builders<DiscoveryQueueItem>.Filter.In(item => item.SourceId, sourceIds))
                    .ToListAsync(insightCancellationToken);

            var discoveredUrls = sourceIds.Length == 0
                ? []
                : await mongoDbContext.DiscoveredUrls
                    .Find(Builders<DiscoveredUrl>.Filter.In(item => item.SourceId, sourceIds))
                    .SortByDescending(item => item.LastSeenUtc)
                    .Limit(10000)
                    .ToListAsync(insightCancellationToken);

            var latestSnapshotsBySourceAndCategory = snapshots
                .GroupBy(snapshot => BuildSourceCategoryKey(snapshot.SourceName, snapshot.CategoryKey), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var latestActivityBySource = crawlLogs
                .GroupBy(log => log.SourceName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            return sources.ToDictionary(
                source => source.Id,
                source => BuildInsights(source, categoriesByKey, latestSnapshotsBySourceAndCategory, latestActivityBySource, discoveryQueueItems, discoveredUrls),
                StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Failed to build source operational insights. Falling back to default summaries.");
            return sources.ToDictionary(
                source => source.Id,
                source => BuildFallbackInsights(source),
                StringComparer.OrdinalIgnoreCase);
        }
    }

    private static SourceOperationalInsights BuildInsights(
        CrawlSource source,
        IReadOnlyDictionary<string, CategoryMetadata> categoriesByKey,
        IReadOnlyDictionary<string, SourceQualitySnapshot> latestSnapshotsBySourceAndCategory,
        IReadOnlyDictionary<string, CrawlLog> latestActivityBySource,
        IReadOnlyList<DiscoveryQueueItem> discoveryQueueItems,
        IReadOnlyList<DiscoveredUrl> discoveredUrls)
    {
        var throughputWindowStartUtc = DateTime.UtcNow.AddHours(-24);
        var assignedCategories = source.SupportedCategoryKeys
            .Select(categoryKey => categoriesByKey.TryGetValue(categoryKey, out var category) ? category : null)
            .Where(category => category is not null)
            .Select(category => category!)
            .ToArray();

        var latestSnapshots = source.SupportedCategoryKeys
            .Select(categoryKey => latestSnapshotsBySourceAndCategory.TryGetValue(BuildSourceCategoryKey(source.DisplayName, categoryKey), out var snapshot) ? snapshot : null)
            .Where(snapshot => snapshot is not null)
            .Select(snapshot => snapshot!)
            .ToArray();

        latestActivityBySource.TryGetValue(source.DisplayName, out var latestActivity);

        var sourceDiscoveryQueueItems = discoveryQueueItems.Where(item => string.Equals(item.SourceId, source.Id, StringComparison.OrdinalIgnoreCase)).ToArray();
        var sourceDiscoveredUrls = discoveredUrls.Where(item => string.Equals(item.SourceId, source.Id, StringComparison.OrdinalIgnoreCase)).ToArray();
        var discoveryCoverageByCategory = source.SupportedCategoryKeys
            .Concat(sourceDiscoveredUrls.Select(item => item.CategoryKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                categoryKey => categoryKey,
                categoryKey => CalculateDiscoveryCompletionPercent(sourceDiscoveredUrls.Where(item => string.Equals(item.CategoryKey, categoryKey, StringComparison.OrdinalIgnoreCase))),
                StringComparer.OrdinalIgnoreCase);

        return new SourceOperationalInsights
        {
            Readiness = BuildReadiness(source, assignedCategories),
            Health = BuildHealth(latestSnapshots),
            LastActivity = latestActivity is null ? null : new SourceLastActivityDto
            {
                TimestampUtc = latestActivity.TimestampUtc,
                Status = string.IsNullOrWhiteSpace(latestActivity.Status) ? "unknown" : latestActivity.Status,
                DurationMs = latestActivity.DurationMs,
                ExtractedProductCount = latestActivity.ExtractedProductCount,
                HadMeaningfulChange = latestActivity.HadMeaningfulChange,
                MeaningfulChangeSummary = latestActivity.MeaningfulChangeSummary,
                ErrorMessage = latestActivity.ErrorMessage
            },
            DiscoveryQueueDepth = sourceDiscoveryQueueItems.Count(IsDiscoveryQueuedOrProcessing),
            ListingPagesVisitedLast24Hours = sourceDiscoveredUrls.Count(item => string.Equals(item.Classification, "listing", StringComparison.OrdinalIgnoreCase)
                && item.LastProcessedUtc is not null
                && item.LastProcessedUtc.Value >= throughputWindowStartUtc),
            SitemapUrlsProcessedLast24Hours = sourceDiscoveredUrls.Count(item => string.Equals(item.Classification, "sitemap", StringComparison.OrdinalIgnoreCase)
                && item.LastProcessedUtc is not null
                && item.LastProcessedUtc.Value >= throughputWindowStartUtc),
            ConfirmedProductUrlsLast24Hours = sourceDiscoveredUrls.Count(item => string.Equals(item.Classification, "product", StringComparison.OrdinalIgnoreCase)
                && item.PromotedToCrawlUtc is not null
                && item.PromotedToCrawlUtc.Value >= throughputWindowStartUtc),
            DiscoveryCoverageByCategory = discoveryCoverageByCategory,
            LastDiscoveryUtc = sourceDiscoveredUrls
                .Select(GetDiscoveryActivityUtc)
                .Where(timestamp => timestamp is not null)
                .Max(),
            SitemapReachable = sourceDiscoveredUrls.Any(item => string.Equals(item.Classification, "sitemap", StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.State, "processed", StringComparison.OrdinalIgnoreCase))
        };
    }

    private static SourceOperationalInsights BuildFallbackInsights(CrawlSource source)
    {
        var assignedCategoryCount = source.SupportedCategoryKeys.Count;
        return new SourceOperationalInsights
        {
            Readiness = new SourceReadinessDto
            {
                Status = assignedCategoryCount == 0 ? "Unassigned" : "Unknown",
                AssignedCategoryCount = assignedCategoryCount,
                CrawlableCategoryCount = 0,
                Summary = assignedCategoryCount == 0
                    ? "No categories are currently assigned."
                    : "Operational insight data is currently unavailable."
            },
            Health = new SourceHealthSummaryDto
            {
                Status = "Unknown"
            },
            LastActivity = null,
            DiscoveryCoverageByCategory = source.SupportedCategoryKeys.ToDictionary(categoryKey => categoryKey, _ => 0m, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static SourceReadinessDto BuildReadiness(CrawlSource source, IReadOnlyList<CategoryMetadata> assignedCategories)
    {
        var assignedCategoryCount = source.SupportedCategoryKeys.Count;
        var crawlableCategoryCount = assignedCategories.Count(category => category.IsEnabled && category.CrawlSupportStatus is CrawlSupportStatus.Supported or CrawlSupportStatus.Experimental);

        if (assignedCategoryCount == 0)
        {
            return new SourceReadinessDto
            {
                Status = "Unassigned",
                AssignedCategoryCount = 0,
                CrawlableCategoryCount = 0,
                Summary = "No categories are currently assigned."
            };
        }

        if (crawlableCategoryCount == assignedCategoryCount)
        {
            return new SourceReadinessDto
            {
                Status = "Ready",
                AssignedCategoryCount = assignedCategoryCount,
                CrawlableCategoryCount = crawlableCategoryCount,
                Summary = $"All {assignedCategoryCount} assigned categories are crawl-ready."
            };
        }

        if (crawlableCategoryCount > 0)
        {
            return new SourceReadinessDto
            {
                Status = "Limited",
                AssignedCategoryCount = assignedCategoryCount,
                CrawlableCategoryCount = crawlableCategoryCount,
                Summary = $"{crawlableCategoryCount} of {assignedCategoryCount} assigned categories are crawl-ready."
            };
        }

        return new SourceReadinessDto
        {
            Status = "Blocked",
            AssignedCategoryCount = assignedCategoryCount,
            CrawlableCategoryCount = 0,
            Summary = "Assigned categories are planned or disabled for crawling."
        };
    }

    private static SourceHealthSummaryDto BuildHealth(IReadOnlyList<SourceQualitySnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return new SourceHealthSummaryDto
            {
                Status = "Unknown"
            };
        }

        var trustScore = decimal.Round(snapshots.Average(snapshot => ToPercent(snapshot.HistoricalTrustScore)), 2, MidpointRounding.AwayFromZero);
        var coveragePercent = decimal.Round(snapshots.Average(snapshot => ToPercent(snapshot.AttributeCoverage)), 2, MidpointRounding.AwayFromZero);
        var successfulCrawlRate = decimal.Round(snapshots.Average(snapshot => ToPercent(snapshot.SuccessfulCrawlRate)), 2, MidpointRounding.AwayFromZero);

        return new SourceHealthSummaryDto
        {
            Status = DetermineHealthStatus(trustScore, successfulCrawlRate),
            TrustScore = trustScore,
            CoveragePercent = coveragePercent,
            SuccessfulCrawlRate = successfulCrawlRate,
            SnapshotUtc = snapshots.Max(snapshot => snapshot.TimestampUtc)
        };
    }

    private static string DetermineHealthStatus(decimal trustScore, decimal successfulCrawlRate)
    {
        if (trustScore >= 85m && successfulCrawlRate >= 85m)
        {
            return "Healthy";
        }

        if (trustScore >= 60m && successfulCrawlRate >= 70m)
        {
            return "Watch";
        }

        return "Attention";
    }

    private static decimal ToPercent(decimal value)
    {
        return value <= 1m ? value * 100m : value;
    }

    private static string BuildSourceCategoryKey(string sourceName, string categoryKey)
    {
        return $"{sourceName.Trim().ToLowerInvariant()}::{categoryKey.Trim().ToLowerInvariant()}";
    }

    private static bool IsDiscoveryQueuedOrProcessing(DiscoveryQueueItem queueItem)
    {
        return string.Equals(queueItem.State, "queued", StringComparison.OrdinalIgnoreCase)
            || string.Equals(queueItem.State, "processing", StringComparison.OrdinalIgnoreCase);
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
        return decimal.Round((decimal)processedCount / totalCount * 100m, 2, MidpointRounding.AwayFromZero);
    }

    private static DateTime? GetDiscoveryActivityUtc(DiscoveredUrl discoveredUrl)
    {
        return new[] { discoveredUrl.LastProcessedUtc, discoveredUrl.PromotedToCrawlUtc, discoveredUrl.LastSeenUtc }
            .Where(timestamp => timestamp is not null)
            .Max();
    }
}