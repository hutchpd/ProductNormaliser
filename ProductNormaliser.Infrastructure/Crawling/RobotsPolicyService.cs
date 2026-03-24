using System.Collections.Concurrent;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Crawling;

public sealed class RobotsPolicyService(IRobotsTxtCache robotsTxtCache, ICrawlSourceStore crawlSourceStore) : IRobotsPolicyService
{
    public async Task<RobotsPolicyDecision> EvaluateAsync(CrawlTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        var uri = new Uri(target.Url, UriKind.Absolute);
        var rules = await robotsTxtCache.GetAsync(target, cancellationToken);
        var path = string.IsNullOrWhiteSpace(uri.PathAndQuery) ? "/" : uri.PathAndQuery;

        if (rules.IsBlocked(path))
        {
            return new RobotsPolicyDecision
            {
                IsAllowed = false,
                Reason = "Blocked by robots policy."
            };
        }

        return new RobotsPolicyDecision
        {
            IsAllowed = true,
            Reason = "Allowed by robots policy."
        };
    }
}