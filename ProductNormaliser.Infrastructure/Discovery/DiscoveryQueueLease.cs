using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Discovery;

public sealed class DiscoveryQueueLease
{
    public string QueueItemId { get; init; } = default!;
    public DiscoveryQueueItem Item { get; init; } = default!;
}