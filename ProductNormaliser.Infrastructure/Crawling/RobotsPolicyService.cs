using System.Collections.Concurrent;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Crawling;

public sealed class RobotsPolicyService(IRobotsTxtCache robotsTxtCache, ICrawlSourceStore crawlSourceStore) : IRobotsPolicyService
{
    public async Task<RobotsPolicyDecision> EvaluateAsync(CrawlTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        var source = await ResolveSourceAsync(target, cancellationToken);
        if (source is not null && !source.ThrottlingPolicy.RespectRobotsTxt)
        {
            return new RobotsPolicyDecision
            {
                IsAllowed = true,
                Reason = "Source is configured to bypass robots policy checks."
            };
        }

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

    private async Task<CrawlSource?> ResolveSourceAsync(CrawlTarget target, CancellationToken cancellationToken)
    {
        if (!target.Metadata.TryGetValue("sourceName", out var sourceName) || string.IsNullOrWhiteSpace(sourceName))
        {
            return null;
        }

        return await crawlSourceStore.GetAsync(sourceName.Trim(), cancellationToken);
    }
}