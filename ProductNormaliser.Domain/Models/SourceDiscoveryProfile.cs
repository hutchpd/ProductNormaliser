namespace ProductNormaliser.Core.Models;

public sealed class SourceDiscoveryProfile
{
    public Dictionary<string, List<string>> CategoryEntryPages { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> SitemapHints { get; set; } = [];
    public List<string> AllowedHosts { get; set; } = [];
    public List<string> AllowedPathPrefixes { get; set; } = [];
    public List<string> ExcludedPathPrefixes { get; set; } = [];
    public List<string> ProductUrlPatterns { get; set; } = [];
    public List<string> ListingUrlPatterns { get; set; } = [];
    public int MaxDiscoveryDepth { get; set; } = 3;
    public int MaxUrlsPerRun { get; set; } = 500;
    public int MaxRetryCount { get; set; } = 3;
    public int RetryBackoffBaseMs { get; set; } = 1000;
    public int RetryBackoffMaxMs { get; set; } = 30000;
}