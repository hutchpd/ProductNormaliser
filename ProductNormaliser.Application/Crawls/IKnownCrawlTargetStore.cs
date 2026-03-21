namespace ProductNormaliser.Application.Crawls;

public interface IKnownCrawlTargetStore
{
    Task<IReadOnlyList<CrawlJobTargetDescriptor>> ListKnownTargetsAsync(
        IReadOnlyCollection<string> categoryKeys,
        IReadOnlyCollection<string> sourceNames,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CrawlJobTargetDescriptor>> ListTargetsForProductsAsync(
        IReadOnlyCollection<string> productIds,
        CancellationToken cancellationToken = default);
}