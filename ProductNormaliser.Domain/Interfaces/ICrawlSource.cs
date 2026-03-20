using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Interfaces;

public interface ICrawlSource
{
    string SourceName { get; }

    Task<IReadOnlyCollection<CrawlTarget>> DiscoverTargetsAsync(CancellationToken cancellationToken);

    Task<FetchResult> FetchAsync(CrawlTarget target, CancellationToken cancellationToken);
}