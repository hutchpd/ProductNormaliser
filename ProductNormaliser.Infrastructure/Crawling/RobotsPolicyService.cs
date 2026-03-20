using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Crawling;

public sealed class RobotsPolicyService(HttpClient httpClient, IOptions<CrawlPipelineOptions> options) : IRobotsPolicyService
{
    private readonly CrawlPipelineOptions crawlOptions = options.Value;
    private readonly ConcurrentDictionary<string, Lazy<Task<RobotsRules>>> rulesCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<RobotsPolicyDecision> EvaluateAsync(CrawlTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        var uri = new Uri(target.Url, UriKind.Absolute);
        var rules = await GetRulesAsync(uri, cancellationToken);
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

    private async Task<RobotsRules> GetRulesAsync(Uri targetUri, CancellationToken cancellationToken)
    {
        var lazyRules = rulesCache.GetOrAdd(
            targetUri.Host,
            _ => new Lazy<Task<RobotsRules>>(() => LoadRulesAsync(targetUri, cancellationToken)));

        return await lazyRules.Value;
    }

    private async Task<RobotsRules> LoadRulesAsync(Uri targetUri, CancellationToken cancellationToken)
    {
        try
        {
            var robotsUri = new UriBuilder(targetUri.Scheme, targetUri.Host, targetUri.Port, "/robots.txt").Uri;
            using var request = new HttpRequestMessage(HttpMethod.Get, robotsUri);
            request.Headers.TryAddWithoutValidation("User-Agent", crawlOptions.UserAgent);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return RobotsRules.AllowAll;
            }

            var robotsText = await response.Content.ReadAsStringAsync(cancellationToken);
            return RobotsRules.Parse(robotsText, crawlOptions.UserAgent);
        }
        catch
        {
            return RobotsRules.AllowAll;
        }
    }

    private sealed class RobotsRules
    {
        public static RobotsRules AllowAll { get; } = new([], []);

        private readonly IReadOnlyList<string> allows;
        private readonly IReadOnlyList<string> disallows;

        private RobotsRules(IReadOnlyList<string> allows, IReadOnlyList<string> disallows)
        {
            this.allows = allows;
            this.disallows = disallows;
        }

        public bool IsBlocked(string path)
        {
            var bestAllowLength = allows
                .Where(rule => path.StartsWith(rule, StringComparison.OrdinalIgnoreCase))
                .Select(rule => rule.Length)
                .DefaultIfEmpty(-1)
                .Max();

            var bestDisallowLength = disallows
                .Where(rule => rule.Length > 0 && path.StartsWith(rule, StringComparison.OrdinalIgnoreCase))
                .Select(rule => rule.Length)
                .DefaultIfEmpty(-1)
                .Max();

            return bestDisallowLength > bestAllowLength;
        }

        public static RobotsRules Parse(string robotsText, string userAgent)
        {
            var allows = new List<string>();
            var disallows = new List<string>();
            var matchingAgent = false;
            var normalisedUserAgent = userAgent.Trim().ToLowerInvariant();

            foreach (var line in robotsText.Split('\n'))
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.Length == 0 || trimmedLine.StartsWith('#'))
                {
                    continue;
                }

                var parts = trimmedLine.Split(':', 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                var directive = parts[0].Trim().ToLowerInvariant();
                var value = parts[1].Trim();

                if (directive == "user-agent")
                {
                    matchingAgent = value == "*" || normalisedUserAgent.Contains(value.Trim().ToLowerInvariant(), StringComparison.Ordinal);
                    continue;
                }

                if (!matchingAgent)
                {
                    continue;
                }

                if (directive == "allow")
                {
                    allows.Add(value);
                }
                else if (directive == "disallow")
                {
                    disallows.Add(value);
                }
            }

            return new RobotsRules(allows, disallows);
        }
    }
}