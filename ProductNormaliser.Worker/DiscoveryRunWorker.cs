using Microsoft.Extensions.Options;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Infrastructure.Crawling;

namespace ProductNormaliser.Worker;

public sealed class DiscoveryRunWorker(
    IDiscoveryRunProcessor discoveryRunProcessor,
    IOptions<CrawlPipelineOptions> options,
    ILogger<DiscoveryRunWorker> logger) : BackgroundService
{
    private readonly CrawlPipelineOptions crawlOptions = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await discoveryRunProcessor.ProcessNextAsync(stoppingToken);
                if (!processed)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(crawlOptions.WorkerIdleDelayMilliseconds), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unhandled discovery run worker failure.");
                await Task.Delay(TimeSpan.FromMilliseconds(crawlOptions.WorkerIdleDelayMilliseconds), stoppingToken);
            }
        }
    }
}