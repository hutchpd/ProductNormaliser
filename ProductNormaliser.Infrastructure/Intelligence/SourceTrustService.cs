using MongoDB.Driver;
using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Normalisation;
using ProductNormaliser.Core.Schemas;
using ProductNormaliser.Infrastructure.Mongo;

namespace ProductNormaliser.Infrastructure.Intelligence;

public sealed class SourceTrustService : ISourceTrustService
{
    private readonly MongoDbContext mongoDbContext;
    private readonly ISourceDisagreementService? sourceDisagreementService;
    private readonly ICategorySchemaRegistry categorySchemaRegistry;
    private readonly ICategoryAttributeNormaliserRegistry categoryAttributeNormaliserRegistry;

    public SourceTrustService(
        MongoDbContext mongoDbContext,
        ISourceDisagreementService? sourceDisagreementService = null,
        ICategorySchemaRegistry? categorySchemaRegistry = null,
        ICategoryAttributeNormaliserRegistry? categoryAttributeNormaliserRegistry = null)
    {
        this.mongoDbContext = mongoDbContext;
        this.sourceDisagreementService = sourceDisagreementService;
        this.categorySchemaRegistry = categorySchemaRegistry ?? new CategorySchemaRegistry([new TvCategorySchemaProvider(), new MonitorCategorySchemaProvider(), new LaptopCategorySchemaProvider(), new RefrigeratorCategorySchemaProvider()]);
        this.categoryAttributeNormaliserRegistry = categoryAttributeNormaliserRegistry ?? new CategoryAttributeNormaliserRegistry([
            new TvAttributeNormaliser(),
            new MonitorAttributeNormaliser(),
            new LaptopAttributeNormaliser(),
            new RefrigeratorAttributeNormaliser()
        ]);
    }

    public decimal GetHistoricalTrustScore(string sourceName, string categoryKey)
    {
        var snapshots = GetSourceHistory(categoryKey, sourceName, limit: 12);
        if (snapshots.Count == 0)
        {
            return 0.72m;
        }

        decimal weightedTotal = 0m;
        decimal weightTotal = 0m;

        for (var index = 0; index < snapshots.Count; index++)
        {
            var weight = Math.Max(1m, snapshots.Count - index);
            weightedTotal += snapshots[index].HistoricalTrustScore * weight;
            weightTotal += weight;
        }

        return Clamp(decimal.Round(weightedTotal / weightTotal, 4, MidpointRounding.AwayFromZero));
    }

    public IReadOnlyList<SourceQualitySnapshot> GetSourceHistory(string categoryKey, string? sourceName = null, int limit = 30)
    {
        return mongoDbContext.SourceQualitySnapshots
            .Find(snapshot => snapshot.CategoryKey == categoryKey && (sourceName == null || snapshot.SourceName == sourceName))
            .SortByDescending(snapshot => snapshot.TimestampUtc)
            .Limit(limit)
            .ToList();
    }

    public void CaptureSnapshot(string sourceName, string categoryKey)
    {
        var snapshot = BuildSnapshot(sourceName, categoryKey);
        mongoDbContext.SourceQualitySnapshots.InsertOne(snapshot);
    }

    private SourceQualitySnapshot BuildSnapshot(string sourceName, string categoryKey)
    {
        var schema = ResolveSchema(categoryKey);
        var sourceProducts = mongoDbContext.SourceProducts
            .Find(product => product.SourceName == sourceName && product.CategoryKey == categoryKey)
            .ToList();
        var relatedCanonicalProducts = mongoDbContext.CanonicalProducts
            .Find(product => product.CategoryKey == categoryKey)
            .ToList()
            .Where(product => product.Sources.Any(source => string.Equals(source.SourceName, sourceName, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var sourceUrls = sourceProducts.Select(product => product.SourceUrl).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var crawlLogs = mongoDbContext.CrawlLogs
            .Find(log => log.SourceName == sourceName && (sourceUrls.Count == 0 || sourceUrls.Contains(log.Url)))
            .SortByDescending(log => log.TimestampUtc)
            .Limit(50)
            .ToList();
        var changeEvents = mongoDbContext.ProductChangeEvents
            .Find(changeEvent => changeEvent.SourceName == sourceName && changeEvent.CategoryKey == categoryKey)
            .SortByDescending(changeEvent => changeEvent.TimestampUtc)
            .Limit(200)
            .ToList();

        var attributeCoverage = CalculateAttributeCoverage(sourceProducts, categoryKey, schema, categoryAttributeNormaliserRegistry);
        var conflictRate = CalculateConflictRate(relatedCanonicalProducts, sourceName);
        var agreementRate = CalculateAgreementRate(relatedCanonicalProducts, sourceName);
        var successfulCrawlRate = CalculateSuccessfulCrawlRate(crawlLogs);
        var priceVolatilityScore = CalculatePriceVolatility(changeEvents);
        var specStabilityScore = CalculateSpecStability(changeEvents);
        var disagreementPenalty = CalculateDisagreementPenalty(categoryKey, sourceName);
        var historicalTrustScore = Clamp(decimal.Round(
            attributeCoverage * 0.22m
            + (1m - conflictRate) * 0.18m
            + agreementRate * 0.25m
            + successfulCrawlRate * 0.20m
            + (1m - priceVolatilityScore) * 0.05m
            + specStabilityScore * 0.10m,
            4,
            MidpointRounding.AwayFromZero) - disagreementPenalty);

        return new SourceQualitySnapshot
        {
            Id = $"source-trust:{sourceName}:{categoryKey}:{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            SourceName = sourceName,
            CategoryKey = categoryKey,
            TimestampUtc = DateTime.UtcNow,
            AttributeCoverage = attributeCoverage,
            ConflictRate = conflictRate,
            AgreementRate = agreementRate,
            SuccessfulCrawlRate = successfulCrawlRate,
            PriceVolatilityScore = priceVolatilityScore,
            SpecStabilityScore = specStabilityScore,
            HistoricalTrustScore = historicalTrustScore
        };
    }

    private static decimal CalculateAttributeCoverage(IReadOnlyCollection<SourceProduct> sourceProducts, string categoryKey, CategorySchema schema, ICategoryAttributeNormaliserRegistry categoryAttributeNormaliserRegistry)
    {
        if (sourceProducts.Count == 0)
        {
            return 0m;
        }

        var evaluationKeys = GetEvaluationKeys(categoryKey, schema, categoryAttributeNormaliserRegistry);
        if (evaluationKeys.Count == 0)
        {
            return 0m;
        }

        var totalCoverage = sourceProducts.Sum(product => Math.Min(1m, decimal.Divide(CountMappedAttributes(product, evaluationKeys), evaluationKeys.Count)));
        return Clamp(decimal.Round(totalCoverage / sourceProducts.Count, 4, MidpointRounding.AwayFromZero));
    }

    private static int CountMappedAttributes(SourceProduct product, IReadOnlySet<string> evaluationKeys)
    {
        var count = 0;
        if (!string.IsNullOrWhiteSpace(product.Brand) && evaluationKeys.Contains("brand"))
        {
            count += 1;
        }

        if (!string.IsNullOrWhiteSpace(product.ModelNumber) && evaluationKeys.Contains("model_number"))
        {
            count += 1;
        }

        if (!string.IsNullOrWhiteSpace(product.Gtin) && evaluationKeys.Contains("gtin"))
        {
            count += 1;
        }

        count += product.NormalisedAttributes.Values.Count(attribute => evaluationKeys.Contains(attribute.AttributeKey) && attribute.Value is not null && (attribute.Value is not string text || !string.IsNullOrWhiteSpace(text)));
        return count;
    }

    private static HashSet<string> GetEvaluationKeys(string categoryKey, CategorySchema schema, ICategoryAttributeNormaliserRegistry categoryAttributeNormaliserRegistry)
    {
        var keys = schema.Attributes
            .Where(attribute => attribute.IsRequired)
            .Select(attribute => attribute.Key)
            .Concat(categoryAttributeNormaliserRegistry.GetCompletenessAttributeKeys(categoryKey))
            .Concat(categoryAttributeNormaliserRegistry.GetIdentityAttributeKeys(categoryKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (keys.Count > 0)
        {
            return keys;
        }

        return schema.Attributes
            .Select(attribute => attribute.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static decimal CalculateConflictRate(IReadOnlyCollection<CanonicalProduct> canonicalProducts, string sourceName)
    {
        var participatingAttributes = canonicalProducts
            .SelectMany(product => product.Attributes.Values)
            .Where(attribute => attribute.Evidence.Any(evidence => string.Equals(evidence.SourceName, sourceName, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (participatingAttributes.Length == 0)
        {
            return 0m;
        }

        return Clamp(decimal.Round((decimal)participatingAttributes.Count(attribute => attribute.HasConflict) / participatingAttributes.Length, 4, MidpointRounding.AwayFromZero));
    }

    private static decimal CalculateAgreementRate(IReadOnlyCollection<CanonicalProduct> canonicalProducts, string sourceName)
    {
        var participatingAttributes = canonicalProducts
            .SelectMany(product => product.Attributes.Values)
            .Where(attribute => attribute.Evidence.Any(evidence => string.Equals(evidence.SourceName, sourceName, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (participatingAttributes.Length == 0)
        {
            return 0.5m;
        }

        var agreementCount = participatingAttributes.Count(attribute =>
            string.Equals(attribute.WinningSourceName, sourceName, StringComparison.OrdinalIgnoreCase)
            || !attribute.HasConflict);

        return Clamp(decimal.Round((decimal)agreementCount / participatingAttributes.Length, 4, MidpointRounding.AwayFromZero));
    }

    private static decimal CalculateSuccessfulCrawlRate(IReadOnlyCollection<CrawlLog> crawlLogs)
    {
        if (crawlLogs.Count == 0)
        {
            return 0.5m;
        }

        var successfulCount = crawlLogs.Count(log => string.Equals(log.Status, "completed", StringComparison.OrdinalIgnoreCase));
        return Clamp(decimal.Round((decimal)successfulCount / crawlLogs.Count, 4, MidpointRounding.AwayFromZero));
    }

    private static decimal CalculatePriceVolatility(IReadOnlyCollection<ProductChangeEvent> changeEvents)
    {
        var priceEvents = changeEvents.Where(changeEvent => string.Equals(changeEvent.AttributeKey, "offer.price", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (priceEvents.Length == 0)
        {
            return 0m;
        }

        var distinctValues = priceEvents.Select(changeEvent => changeEvent.NewValue?.ToString()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        return Clamp(decimal.Round(Math.Min(1m, ((decimal)priceEvents.Length / 12m) * 0.7m + ((decimal)distinctValues / 6m) * 0.3m), 4, MidpointRounding.AwayFromZero));
    }

    private static decimal CalculateSpecStability(IReadOnlyCollection<ProductChangeEvent> changeEvents)
    {
        var specEvents = changeEvents.Where(changeEvent => !changeEvent.AttributeKey.StartsWith("offer.", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (specEvents.Length == 0)
        {
            return 1m;
        }

        var recentEvents = specEvents.Take(40).ToArray();
        var instability = Math.Min(1m, (decimal)recentEvents.Length / 20m);
        return Clamp(decimal.Round(1m - instability, 4, MidpointRounding.AwayFromZero));
    }

    private CategorySchema ResolveSchema(string categoryKey)
    {
        return categorySchemaRegistry.GetSchema(categoryKey)
            ?? new CategorySchema { CategoryKey = categoryKey, DisplayName = categoryKey, Attributes = [] };
    }

    private static decimal Clamp(decimal value)
    {
        return Math.Min(1m, Math.Max(0m, value));
    }

    private decimal CalculateDisagreementPenalty(string categoryKey, string sourceName)
    {
        if (sourceDisagreementService is null)
        {
            return 0m;
        }

        var disagreements = sourceDisagreementService.GetDisagreements(categoryKey, sourceName);
        if (disagreements.Count == 0)
        {
            return 0m;
        }

        var averageDisagreement = disagreements.Average(item => item.DisagreementRate);
        var averageWins = disagreements.Average(item => item.WinRate);
        return Clamp(decimal.Round(averageDisagreement * 0.12m - averageWins * 0.04m, 4, MidpointRounding.AwayFromZero));
    }
}