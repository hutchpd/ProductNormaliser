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
}