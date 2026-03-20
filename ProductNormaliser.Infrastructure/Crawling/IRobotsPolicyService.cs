using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Crawling;

public interface IRobotsPolicyService
{
    Task<RobotsPolicyDecision> EvaluateAsync(CrawlTarget target, CancellationToken cancellationToken);
}