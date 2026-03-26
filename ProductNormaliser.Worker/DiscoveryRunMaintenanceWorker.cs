using Microsoft.Extensions.Options;
using ProductNormaliser.Application.Sources;

namespace ProductNormaliser.Worker;

public sealed class DiscoveryRunMaintenanceWorker(
    DiscoveryRunMaintenanceService discoveryRunMaintenanceService,
    IOptions<DiscoveryRunOperationsOptions> options,
    ILogger<DiscoveryRunMaintenanceWorker> logger) : BackgroundService
{
    private readonly DiscoveryRunOperationsOptions options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await discoveryRunMaintenanceService.SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unhandled discovery run maintenance failure.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, options.MaintenanceSweepIntervalSeconds)), stoppingToken);
        }
    }
}