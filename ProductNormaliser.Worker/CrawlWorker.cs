using Microsoft.Extensions.Options;
using ProductNormaliser.Infrastructure.Crawling;

namespace ProductNormaliser.Worker;

public sealed class CrawlWorker(
    ICrawlQueueService crawlQueueService,
    CrawlOrchestrator crawlOrchestrator,
    IOptions<CrawlPipelineOptions> options,
    ILogger<CrawlWorker> logger) : BackgroundService
{
    private readonly CrawlPipelineOptions crawlOptions = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var lease = await crawlQueueService.DequeueAsync(stoppingToken);
            if (lease is null)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(crawlOptions.WorkerIdleDelayMilliseconds), stoppingToken);
                continue;
            }

            try
            {
                var result = await crawlOrchestrator.ProcessAsync(lease.Target, stoppingToken);

                switch (result.Status)
                {
                    case "completed":
                        await crawlQueueService.MarkCompletedAsync(lease.QueueItemId, stoppingToken);
                        break;
                    case "skipped":
                        await crawlQueueService.MarkSkippedAsync(lease.QueueItemId, result.Message, stoppingToken);
                        break;
                    default:
                        await crawlQueueService.MarkFailedAsync(lease.QueueItemId, result.Message, stoppingToken);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unhandled crawl worker failure for queue item {QueueItemId}", lease.QueueItemId);
                await crawlQueueService.MarkFailedAsync(lease.QueueItemId, exception.Message, stoppingToken);
            }
        }
    }
}