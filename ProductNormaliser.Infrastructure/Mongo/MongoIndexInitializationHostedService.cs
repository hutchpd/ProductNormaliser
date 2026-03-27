using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ProductNormaliser.Infrastructure.Mongo;

internal sealed class MongoIndexInitializationHostedService(
    MongoDbContext context,
    ILogger<MongoIndexInitializationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Ensuring MongoDB indexes for database {DatabaseName}.",
            context.Database.DatabaseNamespace.DatabaseName);

        await context.EnsureIndexesAsync(cancellationToken);

        logger.LogInformation(
            "MongoDB indexes are ready for database {DatabaseName}.",
            context.Database.DatabaseNamespace.DatabaseName);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}