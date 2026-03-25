namespace ProductNormaliser.Core.Models;

public sealed class CrawlSource
{
    public string Id { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string BaseUrl { get; set; } = default!;
    public string Host { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public List<string> AllowedMarkets { get; set; } = ["UK"];
    public string PreferredLocale { get; set; } = "en-GB";
    public SourceAutomationPolicy AutomationPolicy { get; set; } = new();
    public List<string> SupportedCategoryKeys { get; set; } = [];
    public SourceDiscoveryProfile DiscoveryProfile { get; set; } = new();
    public SourceThrottlingPolicy ThrottlingPolicy { get; set; } = new();
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public IReadOnlyCollection<string> GetDiscoveryHosts()
    {
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(Host))
        {
            hosts.Add(NormaliseHost(Host));
        }

        if (Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri))
        {
            hosts.Add(NormaliseHost(baseUri.Host));
        }

        foreach (var allowedHost in DiscoveryProfile.AllowedHosts)
        {
            if (!string.IsNullOrWhiteSpace(allowedHost))
            {
                hosts.Add(NormaliseHost(allowedHost));
            }
        }

        return hosts.ToArray();
    }

    private static string NormaliseHost(string host)
    {
        var trimmed = host.Trim().TrimEnd('.');
        return trimmed.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? trimmed[4..]
            : trimmed;
    }
}