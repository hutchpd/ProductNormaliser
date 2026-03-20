using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Crawling;
using ProductNormaliser.Infrastructure.Mongo.Repositories;

namespace ProductNormaliser.Tests;

public sealed class AdaptiveBackoffServiceTests
{
    [Test]
    public void ComputeNextAttempt_SpeedsUpVolatileImportantPages()
    {
        var service = new AdaptiveCrawlBackoffService(new InMemoryAdaptiveCrawlPolicyStore());
        var now = new DateTime(2026, 03, 20, 12, 00, 00, DateTimeKind.Utc);

        var nextAttempt = service.ComputeNextAttempt(
            new CrawlContext
            {
                SourceName = "alpha",
                CategoryKey = "tv",
                SourceUrl = "https://alpha.example/tv/1",
                ImportanceScore = 0.95m,
                ConsecutiveFailureCount = 0,
                UtcNow = now
            },
            new SourceQualitySnapshot { SourceName = "alpha", CategoryKey = "tv", HistoricalTrustScore = 0.90m },
            new PageVolatilityProfile
            {
                PageVolatilityScore = 0.90m,
                ChangeFrequencyScore = 0.85m,
                PriceVolatilityScore = 0.90m,
                SpecStabilityScore = 0.30m,
                FailureRate = 0m
            });

        Assert.That(nextAttempt, Is.LessThanOrEqualTo(now.AddHours(6)));
    }

    [Test]
    public void ComputeNextAttempt_BacksOffStableFailingPages()
    {
        var service = new AdaptiveCrawlBackoffService(new InMemoryAdaptiveCrawlPolicyStore());
        var now = new DateTime(2026, 03, 20, 12, 00, 00, DateTimeKind.Utc);

        var nextAttempt = service.ComputeNextAttempt(
            new CrawlContext
            {
                SourceName = "beta",
                CategoryKey = "tv",
                SourceUrl = "https://beta.example/tv/1",
                ImportanceScore = 0.20m,
                ConsecutiveFailureCount = 2,
                UtcNow = now
            },
            new SourceQualitySnapshot { SourceName = "beta", CategoryKey = "tv", HistoricalTrustScore = 0.40m },
            new PageVolatilityProfile
            {
                PageVolatilityScore = 0.10m,
                ChangeFrequencyScore = 0.10m,
                PriceVolatilityScore = 0.05m,
                SpecStabilityScore = 0.98m,
                FailureRate = 0.50m
            });

        Assert.That(nextAttempt, Is.GreaterThanOrEqualTo(now.AddDays(7)));
    }

    private sealed class InMemoryAdaptiveCrawlPolicyStore : IAdaptiveCrawlPolicyStore
    {
        private readonly Dictionary<string, AdaptiveCrawlPolicy> policies = new(StringComparer.OrdinalIgnoreCase);

        public Task<AdaptiveCrawlPolicy?> GetAsync(string sourceName, string categoryKey, CancellationToken cancellationToken = default)
            => Task.FromResult(policies.TryGetValue($"{sourceName}:{categoryKey}", out var policy) ? policy : null);

        public Task UpsertAsync(AdaptiveCrawlPolicy policy, CancellationToken cancellationToken = default)
        {
            policies[$"{policy.SourceName}:{policy.CategoryKey}"] = policy;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AdaptiveCrawlPolicy>> ListAsync(string? categoryKey = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AdaptiveCrawlPolicy>>(policies.Values.ToArray());
    }
}