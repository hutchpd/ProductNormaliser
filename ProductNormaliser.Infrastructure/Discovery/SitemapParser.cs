using System.Xml.Linq;

namespace ProductNormaliser.Infrastructure.Discovery;

public sealed class SitemapParser
{
    public SitemapParseResult Parse(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);

        var document = XDocument.Parse(xml, LoadOptions.None);
        var locations = document.Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "loc", StringComparison.OrdinalIgnoreCase))
            .Select(element => element.Value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var rootName = document.Root?.Name.LocalName;
        var isSitemapIndex = string.Equals(rootName, "sitemapindex", StringComparison.OrdinalIgnoreCase)
            || document.Descendants().Any(element => string.Equals(element.Name.LocalName, "sitemap", StringComparison.OrdinalIgnoreCase));
        var isUrlSet = string.Equals(rootName, "urlset", StringComparison.OrdinalIgnoreCase)
            || document.Descendants().Any(element => string.Equals(element.Name.LocalName, "url", StringComparison.OrdinalIgnoreCase));

        return new SitemapParseResult
        {
            ChildSitemaps = isSitemapIndex ? locations : [],
            CandidateUrls = isUrlSet ? locations : []
        };
    }
}

public sealed class SitemapParseResult
{
    public IReadOnlyList<string> ChildSitemaps { get; init; } = [];
    public IReadOnlyList<string> CandidateUrls { get; init; } = [];
    public IReadOnlyList<string> SitemapUrls => ChildSitemaps;
    public IReadOnlyList<string> PageUrls => CandidateUrls;
}