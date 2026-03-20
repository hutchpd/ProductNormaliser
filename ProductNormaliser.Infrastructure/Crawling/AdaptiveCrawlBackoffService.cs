using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Mongo.Repositories;

namespace ProductNormaliser.Infrastructure.Crawling;

public sealed class AdaptiveCrawlBackoffService(IAdaptiveCrawlPolicyStore adaptiveCrawlPolicyStore) : ICrawlBackoffService
{
    public DateTime ComputeNextAttempt(CrawlContext context, SourceQualitySnapshot? sourceHistory, PageVolatilityProfile volatility)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(volatility);

        var policy = BuildPolicy(context, sourceHistory, volatility);
        var baseInterval = decimal.Round((policy.MinIntervalMinutes + policy.MaxIntervalMinutes) / 2m, 0, MidpointRounding.AwayFromZero);
        var trustFactor = policy.TrustMultiplier;
        var volatilityFactor = policy.VolatilityMultiplier;
        var failureFactor = (decimal)Math.Pow((double)policy.FailureBackoffFactor, Math.Max(0, context.ConsecutiveFailureCount));
        var importanceFactor = 1.15m - Clamp(context.ImportanceScore, 0m, 1m) * 0.55m;
        var intervalMinutes = baseInterval * trustFactor * volatilityFactor * failureFactor * importanceFactor;
        intervalMinutes = Clamp(intervalMinutes, policy.MinIntervalMinutes, policy.MaxIntervalMinutes);

        return context.UtcNow.AddMinutes((double)intervalMinutes);
    }

    public AdaptiveCrawlPolicy BuildPolicy(CrawlContext context, SourceQualitySnapshot? sourceHistory, PageVolatilityProfile volatility)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(volatility);

        var trustScore = sourceHistory?.HistoricalTrustScore ?? 0.60m;
        var volatilityScore = Clamp((volatility.PageVolatilityScore + volatility.ChangeFrequencyScore + volatility.PriceVolatilityScore) / 3m, 0m, 1m);

        var minIntervalMinutes = volatility.PriceVolatilityScore >= 0.65m || volatility.ChangeFrequencyScore >= 0.70m
            ? 60
            : volatility.SpecStabilityScore >= 0.90m
                ? 24 * 60
                : 6 * 60;
        var maxIntervalMinutes = volatility.SpecStabilityScore >= 0.92m && volatility.ChangeFrequencyScore <= 0.20m
            ? 30 * 24 * 60
            : volatility.PageVolatilityScore >= 0.60m
                ? 12 * 60
                : 7 * 24 * 60;

        var trustMultiplier = trustScore switch
        {
            >= 0.85m => 0.75m,
            >= 0.70m => 0.90m,
            >= 0.50m => 1.10m,
            _ => 1.40m
        };
        var volatilityMultiplier = volatilityScore switch
        {
            >= 0.80m => 0.25m,
            >= 0.60m => 0.45m,
            >= 0.40m => 0.75m,
            _ => 1.30m
        };
        var failureBackoffFactor = 1.8m + volatility.FailureRate * 1.2m;

        var policy = new AdaptiveCrawlPolicy
        {
            Id = $"policy:{context.SourceName}:{context.CategoryKey}",
            SourceName = context.SourceName,
            CategoryKey = context.CategoryKey,
            MinIntervalMinutes = minIntervalMinutes,
            MaxIntervalMinutes = Math.Max(minIntervalMinutes, maxIntervalMinutes),
            VolatilityMultiplier = volatilityMultiplier,
            TrustMultiplier = trustMultiplier,
            FailureBackoffFactor = decimal.Round(failureBackoffFactor, 2, MidpointRounding.AwayFromZero),
            LastComputedUtc = context.UtcNow
        };

        adaptiveCrawlPolicyStore.UpsertAsync(policy).GetAwaiter().GetResult();
        return policy;
    }

    private static decimal Clamp(decimal value, decimal min, decimal max)
    {
        return Math.Min(max, Math.Max(min, value));
    }
}