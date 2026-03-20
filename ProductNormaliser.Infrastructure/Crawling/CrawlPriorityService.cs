using MongoDB.Driver;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;
using ProductNormaliser.Infrastructure.Mongo;

namespace ProductNormaliser.Infrastructure.Crawling;

public sealed class CrawlPriorityService(MongoDbContext mongoDbContext) : ICrawlPriorityService
{
    public async Task<IReadOnlyList<CrawlPriorityAssessment>> GetPrioritiesAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        var queueItems = await mongoDbContext.CrawlQueueItems.Find(item => item.Status == "queued" && (item.NextAttemptUtc == null || item.NextAttemptUtc <= utcNow))
            .ToListAsync(cancellationToken);

        var sourceProducts = await mongoDbContext.SourceProducts.Find(Builders<SourceProduct>.Filter.Empty).ToListAsync(cancellationToken);
        var canonicalProducts = await mongoDbContext.CanonicalProducts.Find(Builders<CanonicalProduct>.Filter.Empty).ToListAsync(cancellationToken);
        var crawlLogs = await mongoDbContext.CrawlLogs.Find(Builders<CrawlLog>.Filter.Empty)
            .SortByDescending(log => log.TimestampUtc)
            .Limit(500)
            .ToListAsync(cancellationToken);

        var assessments = new List<CrawlPriorityAssessment>(queueItems.Count);

        foreach (var queueItem in queueItems)
        {
            var sourceQualityScore = CalculateSourceQuality(queueItem.SourceName, sourceProducts);
            var changeFrequencyScore = CalculateChangeFrequency(queueItem, crawlLogs);
            var (missingAttributeCount, missingAttributeScore) = CalculateMissingAttributeSignal(queueItem, sourceProducts, canonicalProducts);
            var (stalenessScore, lastCrawledUtc) = CalculateStaleness(queueItem, utcNow, crawlLogs);
            var priorityScore = decimal.Round(
                sourceQualityScore * 35m
                + changeFrequencyScore * 25m
                + missingAttributeScore * 30m
                + stalenessScore * 10m,
                2,
                MidpointRounding.AwayFromZero);

            assessments.Add(new CrawlPriorityAssessment
            {
                QueueItem = queueItem,
                PriorityScore = priorityScore,
                SourceQualityScore = decimal.Round(sourceQualityScore * 100m, 2, MidpointRounding.AwayFromZero),
                ChangeFrequencyScore = decimal.Round(changeFrequencyScore * 100m, 2, MidpointRounding.AwayFromZero),
                MissingAttributeScore = decimal.Round(missingAttributeScore * 100m, 2, MidpointRounding.AwayFromZero),
                StalenessScore = decimal.Round(stalenessScore * 100m, 2, MidpointRounding.AwayFromZero),
                MissingAttributeCount = missingAttributeCount,
                LastCrawledUtc = lastCrawledUtc,
                Reasons = BuildReasons(sourceQualityScore, changeFrequencyScore, missingAttributeCount, stalenessScore)
            });
        }

        return assessments
            .OrderByDescending(assessment => assessment.PriorityScore)
            .ThenByDescending(assessment => assessment.MissingAttributeCount)
            .ThenBy(assessment => assessment.QueueItem.EnqueuedUtc)
            .ToArray();
    }

    private static decimal CalculateSourceQuality(string sourceName, IReadOnlyCollection<SourceProduct> sourceProducts)
    {
        var products = sourceProducts.Where(product => string.Equals(product.SourceName, sourceName, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (products.Length == 0)
        {
            return 0.45m;
        }

        var averageConfidence = products
            .SelectMany(product => product.NormalisedAttributes.Values)
            .DefaultIfEmpty(new NormalisedAttributeValue { Confidence = 0.70m })
            .Average(attribute => attribute.Confidence);
        var completeness = products.Average(product => Math.Min(1.00m, (decimal)product.NormalisedAttributes.Count / 8m));
        return Clamp(decimal.Round(averageConfidence * 0.65m + completeness * 0.35m, 4, MidpointRounding.AwayFromZero));
    }

    private static decimal CalculateChangeFrequency(CrawlQueueItem queueItem, IReadOnlyCollection<CrawlLog> crawlLogs)
    {
        var recentLogs = crawlLogs
            .Where(log => string.Equals(log.SourceName, queueItem.SourceName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(log.Url, queueItem.SourceUrl, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .ToArray();

        if (recentLogs.Length == 0)
        {
            return 0.50m;
        }

        var changeCount = recentLogs.Count(log => log.HadMeaningfulChange);
        return Clamp(decimal.Round((decimal)changeCount / recentLogs.Length, 4, MidpointRounding.AwayFromZero));
    }

    private static (int MissingAttributeCount, decimal MissingAttributeScore) CalculateMissingAttributeSignal(
        CrawlQueueItem queueItem,
        IReadOnlyCollection<SourceProduct> sourceProducts,
        IReadOnlyCollection<CanonicalProduct> canonicalProducts)
    {
        var sourceProduct = sourceProducts.FirstOrDefault(product =>
            string.Equals(product.SourceName, queueItem.SourceName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(product.SourceUrl, queueItem.SourceUrl, StringComparison.OrdinalIgnoreCase));

        if (sourceProduct is null)
        {
            return (3, 0.60m);
        }

        var canonicalProduct = canonicalProducts.FirstOrDefault(product => product.Sources.Any(source =>
            string.Equals(source.SourceName, queueItem.SourceName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(source.SourceUrl, queueItem.SourceUrl, StringComparison.OrdinalIgnoreCase)));

        if (canonicalProduct is null)
        {
            return (2, 0.50m);
        }

        var schema = GetSchema(queueItem.CategoryKey);
        if (schema.Attributes.Count == 0)
        {
            return (0, 0m);
        }

        var missingCount = schema.Attributes.Count(attribute => IsMissing(canonicalProduct, attribute.Key));
        return (missingCount, Clamp(decimal.Round((decimal)missingCount / schema.Attributes.Count, 4, MidpointRounding.AwayFromZero)));
    }

    private static (decimal StalenessScore, DateTime? LastCrawledUtc) CalculateStaleness(CrawlQueueItem queueItem, DateTime utcNow, IReadOnlyCollection<CrawlLog> crawlLogs)
    {
        var lastLog = crawlLogs.FirstOrDefault(log =>
            string.Equals(log.SourceName, queueItem.SourceName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(log.Url, queueItem.SourceUrl, StringComparison.OrdinalIgnoreCase));

        if (lastLog is null)
        {
            return (1.00m, null);
        }

        var ageDays = (decimal)Math.Max(0d, (utcNow - lastLog.TimestampUtc).TotalDays);
        var staleness = ageDays switch
        {
            >= 30m => 1.00m,
            >= 14m => 0.85m,
            >= 7m => 0.70m,
            >= 1m => 0.45m,
            _ => 0.20m
        };

        return (staleness, lastLog.TimestampUtc);
    }

    private static IReadOnlyList<string> BuildReasons(decimal sourceQuality, decimal changeFrequency, int missingAttributeCount, decimal staleness)
    {
        var reasons = new List<string>();
        if (sourceQuality >= 0.70m)
        {
            reasons.Add("High-value source quality");
        }

        if (changeFrequency >= 0.60m)
        {
            reasons.Add("Frequently changing page");
        }

        if (missingAttributeCount > 0)
        {
            reasons.Add($"Canonical product missing {missingAttributeCount} attributes");
        }

        if (staleness >= 0.70m)
        {
            reasons.Add("Page is stale and due for refresh");
        }

        return reasons.Count == 0 ? ["Baseline recrawl priority"] : reasons;
    }

    private static CategorySchema GetSchema(string categoryKey)
    {
        return string.Equals(categoryKey, TvCategorySchemaProvider.CategoryKey, StringComparison.OrdinalIgnoreCase)
            ? new TvCategorySchemaProvider().GetSchema()
            : new CategorySchema { CategoryKey = categoryKey, DisplayName = categoryKey, Attributes = [] };
    }

    private static bool IsMissing(CanonicalProduct product, string key)
    {
        return key switch
        {
            "brand" => string.IsNullOrWhiteSpace(product.Brand),
            "model_number" => string.IsNullOrWhiteSpace(product.ModelNumber),
            "gtin" => string.IsNullOrWhiteSpace(product.Gtin),
            _ => !product.Attributes.TryGetValue(key, out var attribute) || attribute.Value is null || (attribute.Value is string text && string.IsNullOrWhiteSpace(text))
        };
    }

    private static decimal Clamp(decimal value)
    {
        return Math.Min(1.00m, Math.Max(0m, value));
    }
}