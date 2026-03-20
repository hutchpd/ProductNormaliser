using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ProductNormaliser.Infrastructure.Mongo.Repositories;

namespace ProductNormaliser.Infrastructure.Mongo;

public static class MongoServiceCollectionExtensions
{
    public static IServiceCollection AddProductNormaliserMongo(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MongoSettings>()
            .Bind(configuration.GetSection(MongoSettings.SectionName))
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.ConnectionString), "Mongo connection string is required.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.DatabaseName), "Mongo database name is required.");

        services.AddSingleton<IMongoClient>(serviceProvider =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<MongoSettings>>().Value;
            return new MongoClient(settings.ConnectionString);
        });

        services.AddSingleton(serviceProvider =>
        {
            var mongoClient = serviceProvider.GetRequiredService<IMongoClient>();
            var settings = serviceProvider.GetRequiredService<IOptions<MongoSettings>>().Value;
            return new MongoDbContext(mongoClient, settings.DatabaseName);
        });

        services.AddSingleton<RawPageRepository>();
        services.AddSingleton<SourceProductRepository>();
        services.AddSingleton<CanonicalProductRepository>();
        services.AddSingleton<ProductOfferRepository>();
        services.AddSingleton<MergeConflictRepository>();
        services.AddSingleton<CrawlQueueRepository>();

        return services;
    }
}