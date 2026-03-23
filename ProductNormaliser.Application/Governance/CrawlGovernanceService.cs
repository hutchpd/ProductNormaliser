using System.Net;
using Microsoft.Extensions.Options;
using ProductNormaliser.Application.Crawls;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Governance;

public sealed class CrawlGovernanceService(IOptions<CrawlGovernanceOptions> options) : ICrawlGovernanceService
{
    private readonly CrawlGovernanceOptions governanceOptions = options.Value;

    public void ValidateSourceBaseUrl(string baseUrl, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl, parameterName);

        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Base URL must be an absolute HTTP or HTTPS URL.", parameterName);
        }

        var host = uri.Host.Trim().ToLowerInvariant();
        if (MatchesAny(host, governanceOptions.BlockedDomains))
        {
            throw new ArgumentException($"Domain '{host}' is blocked by crawl governance rules.", parameterName);
        }

        if (governanceOptions.AllowedDomains.Count > 0 && !MatchesAny(host, governanceOptions.AllowedDomains))
        {
            throw new ArgumentException($"Domain '{host}' is outside the configured crawl allow-list.", parameterName);
        }

        if (!governanceOptions.AllowPrivateNetworkTargets && IsPrivateOrLocalHost(host))
        {
            throw new ArgumentException($"Domain '{host}' resolves to a local or private-network target and is not allowed.", parameterName);
        }
    }

    public void ValidateCrawlRequest(
        string requestType,
        IReadOnlyCollection<string> categories,
        IReadOnlyCollection<string> sources,
        IReadOnlyCollection<string> productIds,
        IReadOnlyCollection<CrawlJobTargetDescriptor> targets,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestType, parameterName);
        ArgumentNullException.ThrowIfNull(targets);

        if (targets.Count > governanceOptions.MaxTargetsPerJob)
        {
            throw new ArgumentException(
                $"Requested crawl expands to {targets.Count} targets, which exceeds the configured maximum of {governanceOptions.MaxTargetsPerJob}.",
                parameterName);
        }

        if (governanceOptions.RequireExplicitSourcesForLargeCategoryCrawls
            && string.Equals(requestType, CrawlJobRequestTypes.Category, StringComparison.OrdinalIgnoreCase)
            && sources.Count == 0
            && targets.Count > governanceOptions.LargeCrawlThreshold)
        {
            throw new ArgumentException(
                $"This crawl expands to {targets.Count} targets. Select one or more sources before launching large category-wide crawls.",
                parameterName);
        }

        _ = categories;
        _ = productIds;
    }

    private static bool MatchesAny(string host, IReadOnlyCollection<string> rules)
    {
        return rules.Any(rule => MatchesDomainRule(host, rule));
    }

    private static bool MatchesDomainRule(string host, string rule)
    {
        if (string.IsNullOrWhiteSpace(rule))
        {
            return false;
        }

        var normalizedRule = rule.Trim().TrimStart('.').ToLowerInvariant();
        return string.Equals(host, normalizedRule, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith($".{normalizedRule}", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPrivateOrLocalHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(host, out var address))
        {
            return false;
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] switch
            {
                10 => true,
                127 => true,
                169 when bytes[1] == 254 => true,
                172 when bytes[1] >= 16 && bytes[1] <= 31 => true,
                192 when bytes[1] == 168 => true,
                _ => false
            };
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
            {
                return true;
            }

            var bytes = address.GetAddressBytes();
            return (bytes[0] & 0xfe) == 0xfc;
        }

        return false;
    }
}