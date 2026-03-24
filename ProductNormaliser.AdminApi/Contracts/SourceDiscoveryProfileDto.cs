namespace ProductNormaliser.AdminApi.Contracts;

public sealed class SourceDiscoveryProfileDto
{
    public IReadOnlyDictionary<string, IReadOnlyList<string>> CategoryEntryPages { get; init; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> SitemapHints { get; init; } = [];
    public IReadOnlyList<string> AllowedHosts { get; init; } = [];
    public IReadOnlyList<string> AllowedPathPrefixes { get; init; } = [];
    public IReadOnlyList<string> ExcludedPathPrefixes { get; init; } = [];
    public IReadOnlyList<string> ProductUrlPatterns { get; init; } = [];
    public IReadOnlyList<string> ListingUrlPatterns { get; init; } = [];
    public int MaxDiscoveryDepth { get; init; }
    public int MaxUrlsPerRun { get; init; }
    public int MaxRetryCount { get; init; }
    public int RetryBackoffBaseMs { get; init; }
    public int RetryBackoffMaxMs { get; init; }
}