using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Crawling;

public sealed class CrawlQueueLease
{
    public string QueueItemId { get; init; } = default!;
    public CrawlTarget Target { get; init; } = default!;
}