using MongoDB.Driver;
using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;
using ProductNormaliser.Infrastructure.Mongo;

namespace ProductNormaliser.Infrastructure.Intelligence;

public sealed class SourceTrustService : ISourceTrustService
{
    private readonly MongoDbContext mongoDbContext;
    private readonly ISourceDisagreementService? sourceDisagreementService;

    public SourceTrustService(MongoDbContext mongoDbContext, ISourceDisagreementService? sourceDisagreementService = null)
    {
        this.mongoDbContext = mongoDbContext;
        this.sourceDisagreementService = sourceDisagreementService;
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
        var schema = GetSchema(categoryKey);
        var sourceProducts = mongoDbContext.SourceProducts
            .Find(product => product.SourceName == sourceName && product.CategoryKey == categoryKey)
            .ToList();
        var relatedCanonicalProducts = mongoDbContext.CanonicalProducts
            .Find(product => product.CategoryKey == categoryKey)
            .ToList()
            .Where(product => product.Sources.Any(source => string.Equals(source.SourceName, sourceName, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var crawlLogs = mongoDbContext.CrawlLogs
            .Find(log => log.SourceName == sourceName)
            .SortByDescending(log => log.TimestampUtc)
            .Limit(50)
            .ToList();
        var changeEvents = mongoDbContext.ProductChangeEvents
            .Find(changeEvent => changeEvent.SourceName == sourceName && changeEvent.CategoryKey == categoryKey)
            .SortByDescending(changeEvent => changeEvent.TimestampUtc)
            .Limit(200)
            .ToList();

        var attributeCoverage = CalculateAttributeCoverage(sourceProducts, schema);
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

    private static decimal CalculateAttributeCoverage(IReadOnlyCollection<SourceProduct> sourceProducts, CategorySchema schema)
    {
        if (sourceProducts.Count == 0 || schema.Attributes.Count == 0)
        {
            return 0m;
        }

        var totalCoverage = sourceProducts.Sum(product => Math.Min(1m, (decimal)CountMappedAttributes(product) / schema.Attributes.Count));
        return Clamp(decimal.Round(totalCoverage / sourceProducts.Count, 4, MidpointRounding.AwayFromZero));
    }

    private static int CountMappedAttributes(SourceProduct product)
    {
        var count = product.NormalisedAttributes.Values.Count(attribute => attribute.Value is not null);
        if (!string.IsNullOrWhiteSpace(product.Brand))
        {
            count += 1;
        }

        if (!string.IsNullOrWhiteSpace(product.ModelNumber))
        {
            count += 1;
        }

        if (!string.IsNullOrWhiteSpace(product.Gtin))
        {
            count += 1;
        }

        return count;
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

    private static CategorySchema GetSchema(string categoryKey)
    {
        return string.Equals(categoryKey, TvCategorySchemaProvider.CategoryKey, StringComparison.OrdinalIgnoreCase)
            ? new TvCategorySchemaProvider().GetSchema()
            : new CategorySchema { CategoryKey = categoryKey, DisplayName = categoryKey, Attributes = [] };
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