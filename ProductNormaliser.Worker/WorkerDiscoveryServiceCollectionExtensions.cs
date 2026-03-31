using Microsoft.Extensions.DependencyInjection;
using ProductNormaliser.Application.Sources;

namespace ProductNormaliser.Worker;

public static class WorkerDiscoveryServiceCollectionExtensions
{
    public static IServiceCollection AddWorkerDiscoveryServices(this IServiceCollection services)
    {
        services.AddSingleton<IDiscoveryRunService, DiscoveryRunService>();
        services.AddSingleton<IDiscoveryRunProcessor, DiscoveryRunProcessor>();
        services.AddSingleton<DiscoveryRunMaintenanceService>();
        return services;
    }
}