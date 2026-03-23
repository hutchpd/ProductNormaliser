using Microsoft.Extensions.Options;
using ProductNormaliser.Infrastructure.Crawling;
using ProductNormaliser.Infrastructure.Discovery;

namespace ProductNormaliser.Worker;

public sealed class DiscoveryWorker(
    DiscoverySeedService discoverySeedService,
    IDiscoveryQueueService discoveryQueueService,
    DiscoveryOrchestrator discoveryOrchestrator,
    IOptions<CrawlPipelineOptions> options,
    ILogger<DiscoveryWorker> logger) : BackgroundService
{
    private readonly CrawlPipelineOptions crawlOptions = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await discoverySeedService.EnsureSeededAsync(stoppingToken);

            var lease = await discoveryQueueService.DequeueAsync(stoppingToken);
            if (lease is null)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(crawlOptions.WorkerIdleDelayMilliseconds), stoppingToken);
                continue;
            }

            try
            {
                var result = await discoveryOrchestrator.ProcessAsync(lease.Item, stoppingToken);
                switch (result.Status)
                {
                    case "completed":
                        await discoveryQueueService.MarkCompletedAsync(lease.QueueItemId, stoppingToken);
                        break;
                    case "skipped":
                        await discoveryQueueService.MarkSkippedAsync(lease.QueueItemId, result.Message, stoppingToken);
                        break;
                    default:
                        await discoveryQueueService.MarkFailedAsync(lease.QueueItemId, result.Message, stoppingToken);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unhandled discovery worker failure for queue item {QueueItemId}", lease.QueueItemId);
                await discoveryQueueService.MarkFailedAsync(lease.QueueItemId, exception.Message, stoppingToken);
            }
        }
    }
}