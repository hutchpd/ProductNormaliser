using ProductNormaliser.Application.Crawls;

namespace ProductNormaliser.Application.Governance;

public interface ICrawlGovernanceService
{
    void ValidateSourceBaseUrl(string baseUrl, string parameterName);

    void ValidateCrawlRequest(
        string requestType,
        IReadOnlyCollection<string> categories,
        IReadOnlyCollection<string> sources,
        IReadOnlyCollection<string> productIds,
        IReadOnlyCollection<CrawlJobTargetDescriptor> targets,
        string parameterName);
}