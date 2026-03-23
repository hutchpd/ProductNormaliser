namespace ProductNormaliser.Application.Governance;

public sealed class CrawlGovernanceOptions
{
    public const string SectionName = "CrawlGovernance";

    public IReadOnlyList<string> AllowedDomains { get; init; } = [];

    public IReadOnlyList<string> BlockedDomains { get; init; } = [];

    public bool AllowPrivateNetworkTargets { get; init; }

    public int LargeCrawlThreshold { get; init; } = 100;

    public int MaxTargetsPerJob { get; init; } = 500;

    public bool RequireExplicitSourcesForLargeCategoryCrawls { get; init; } = true;
}