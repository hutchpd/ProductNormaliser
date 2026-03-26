using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Discovery;

public interface IDiscoveryLinkPolicy
{
    string NormalizeUrl(string url);
    bool TryNormalizeAndValidate(CrawlSource source, string categoryKey, string candidateUrl, int depth, out string normalizedUrl);
    bool IsAllowed(CrawlSource source, string categoryKey, string url, int depth);
}