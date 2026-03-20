using MongoDB.Driver;
using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;
using ProductNormaliser.Infrastructure.Mongo;

namespace ProductNormaliser.Infrastructure.Intelligence;

public sealed class AttributeStabilityService(MongoDbContext mongoDbContext, ICategorySchemaRegistry? categorySchemaRegistry = null) : IAttributeStabilityService
{
    private readonly ICategorySchemaRegistry categorySchemaRegistry = categorySchemaRegistry ?? new CategorySchemaRegistry([new TvCategorySchemaProvider(), new MonitorCategorySchemaProvider(), new LaptopCategorySchemaProvider(), new RefrigeratorCategorySchemaProvider()]);

    public decimal GetStabilityScore(string categoryKey, string attributeKey)
    {
        return GetScores(categoryKey)
            .FirstOrDefault(score => string.Equals(score.AttributeKey, attributeKey, StringComparison.OrdinalIgnoreCase))?
            .StabilityScore
            ?? 0.90m;
    }

    public IReadOnlyList<AttributeStabilityScore> GetScores(string categoryKey)
    {
        var schema = ResolveSchema(categoryKey);
        var events = mongoDbContext.ProductChangeEvents
            .Find(changeEvent => changeEvent.CategoryKey == categoryKey)
            .SortByDescending(changeEvent => changeEvent.TimestampUtc)
            .Limit(1000)
            .ToList();

        var keys = schema.Attributes.Select(attribute => attribute.Key)
            .Union(events.Select(changeEvent => changeEvent.AttributeKey), StringComparer.OrdinalIgnoreCase)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var scores = new List<AttributeStabilityScore>();
        foreach (var key in keys)
        {
            var attributeEvents = events
                .Where(changeEvent => string.Equals(changeEvent.AttributeKey, key, StringComparison.OrdinalIgnoreCase))
                .OrderBy(changeEvent => changeEvent.TimestampUtc)
                .ToArray();

            var changeCount = attributeEvents.Length;
            var distinctValueCount = attributeEvents
                .SelectMany(changeEvent => new[] { changeEvent.OldValue?.ToString(), changeEvent.NewValue?.ToString() })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            var oscillationCount = CountOscillations(attributeEvents);
            var (isSuspicious, suspicionReason) = DetectSuspicion(categoryKey, key, attributeEvents, oscillationCount);
            var stabilityScore = CalculateStability(changeCount, oscillationCount, distinctValueCount, isSuspicious);

            scores.Add(new AttributeStabilityScore
            {
                CategoryKey = categoryKey,
                AttributeKey = key,
                ChangeCount = changeCount,
                OscillationCount = oscillationCount,
                DistinctValueCount = distinctValueCount,
                StabilityScore = stabilityScore,
                IsSuspicious = isSuspicious,
                SuspicionReason = suspicionReason
            });
        }

        return scores
            .OrderBy(score => score.StabilityScore)
            .ThenByDescending(score => score.ChangeCount)
            .ThenBy(score => score.AttributeKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static int CountOscillations(IReadOnlyList<ProductChangeEvent> changeEvents)
    {
        var oscillations = 0;
        for (var index = 2; index < changeEvents.Count; index++)
        {
            var firstValue = changeEvents[index - 2].NewValue?.ToString();
            var secondValue = changeEvents[index - 1].NewValue?.ToString();
            var thirdValue = changeEvents[index].NewValue?.ToString();

            if (!string.IsNullOrWhiteSpace(firstValue)
                && !string.IsNullOrWhiteSpace(secondValue)
                && !string.IsNullOrWhiteSpace(thirdValue)
                && string.Equals(firstValue, thirdValue, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(firstValue, secondValue, StringComparison.OrdinalIgnoreCase))
            {
                oscillations += 1;
            }
        }

        return oscillations;
    }

    private static decimal CalculateStability(int changeCount, int oscillationCount, int distinctValueCount, bool isSuspicious)
    {
        var instability = Math.Min(1m,
            changeCount / 20m * 0.55m
            + oscillationCount / 6m * 0.30m
            + distinctValueCount / 8m * 0.15m);

        var stabilityScore = Math.Max(0.05m, decimal.Round(1m - instability, 4, MidpointRounding.AwayFromZero));
        return isSuspicious ? Math.Min(0.40m, stabilityScore) : stabilityScore;
    }

    private static (bool IsSuspicious, string? Reason) DetectSuspicion(string categoryKey, string attributeKey, IReadOnlyList<ProductChangeEvent> changeEvents, int oscillationCount)
    {
        if (oscillationCount >= 2)
        {
            return (true, "Value is oscillating across repeated updates.");
        }

        foreach (var changeEvent in changeEvents)
        {
            if (IsImpossibleValue(categoryKey, attributeKey, changeEvent.NewValue))
            {
                return (true, "Impossible or highly implausible value observed.");
            }
        }

        return (false, null);
    }

    private static bool IsImpossibleValue(string categoryKey, string attributeKey, object? value)
    {
        if (value is null || !decimal.TryParse(value.ToString(), out var numericValue))
        {
            return false;
        }

        return (categoryKey, attributeKey) switch
        {
            ("tv", "refresh_rate_hz") => numericValue > 240m || numericValue < 24m,
            ("tv", "screen_size_inch") => numericValue > 120m || numericValue < 10m,
            ("tv", "hdmi_port_count") => numericValue > 12m || numericValue < 0m,
            ("monitor", "refresh_rate_hz") => numericValue > 500m || numericValue < 24m,
            ("monitor", "screen_size_inch") => numericValue > 60m || numericValue < 10m,
            ("monitor", "displayport_port_count") => numericValue > 6m || numericValue < 0m,
            ("laptop", "ram_gb") => numericValue > 512m || numericValue < 2m,
            ("laptop", "storage_capacity_gb") => numericValue > 8000m || numericValue < 32m,
            ("laptop", "display_size_inch") => numericValue > 20m || numericValue < 10m,
            ("laptop", "weight_kg") => numericValue > 10m || numericValue < 0.5m,
            ("refrigerator", "total_capacity_litre") => numericValue > 1500m || numericValue < 40m,
            ("refrigerator", "fridge_capacity_litre") => numericValue > 1200m || numericValue < 20m,
            ("refrigerator", "freezer_capacity_litre") => numericValue > 800m || numericValue < 0m,
            ("refrigerator", "width_mm") => numericValue > 2500m || numericValue < 300m,
            ("refrigerator", "height_mm") => numericValue > 2500m || numericValue < 600m,
            ("refrigerator", "depth_mm") => numericValue > 1500m || numericValue < 300m,
            _ => false
        };
    }

    private CategorySchema ResolveSchema(string categoryKey)
    {
        return categorySchemaRegistry.GetSchema(categoryKey)
            ?? new CategorySchema { CategoryKey = categoryKey, DisplayName = categoryKey, Attributes = [] };
    }
}