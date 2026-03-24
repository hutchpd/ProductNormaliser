using ProductNormaliser.AdminApi.Contracts;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.AdminApi.Services;

public interface ISourceOperationalInsightsProvider
{
    Task<IReadOnlyDictionary<string, SourceOperationalInsights>> BuildAsync(IReadOnlyList<CrawlSource> sources, CancellationToken cancellationToken);
}

public sealed class SourceOperationalInsights
{
    public SourceReadinessDto Readiness { get; init; } = new();
    public SourceHealthSummaryDto Health { get; init; } = new();
    public SourceLastActivityDto? LastActivity { get; init; }
    public int DiscoveryQueueDepth { get; init; }
    public int ListingPagesVisitedLast24Hours { get; init; }
    public int SitemapUrlsProcessedLast24Hours { get; init; }
    public int ConfirmedProductUrlsLast24Hours { get; init; }
    public IReadOnlyDictionary<string, decimal> DiscoveryCoverageByCategory { get; init; } = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
    public DateTime? LastDiscoveryUtc { get; init; }
    public bool SitemapReachable { get; init; }
}