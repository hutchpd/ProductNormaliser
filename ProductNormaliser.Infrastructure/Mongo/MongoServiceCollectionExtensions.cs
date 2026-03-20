using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ProductNormaliser.Infrastructure.Crawling;
using ProductNormaliser.Infrastructure.Mongo.Repositories;
using ProductNormaliser.Infrastructure.StructuredData;

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
        services.AddSingleton<CrawlLogRepository>();
        services.AddSingleton<IRawPageStore>(serviceProvider => serviceProvider.GetRequiredService<RawPageRepository>());
        services.AddSingleton<ISourceProductStore>(serviceProvider => serviceProvider.GetRequiredService<SourceProductRepository>());
        services.AddSingleton<ICanonicalProductStore>(serviceProvider => serviceProvider.GetRequiredService<CanonicalProductRepository>());
        services.AddSingleton<IProductOfferStore>(serviceProvider => serviceProvider.GetRequiredService<ProductOfferRepository>());
        services.AddSingleton<IMergeConflictStore>(serviceProvider => serviceProvider.GetRequiredService<MergeConflictRepository>());
        services.AddSingleton<ICrawlQueueStore>(serviceProvider => serviceProvider.GetRequiredService<CrawlQueueRepository>());
        services.AddSingleton<ICrawlLogStore>(serviceProvider => serviceProvider.GetRequiredService<CrawlLogRepository>());

        services.AddOptions<CrawlPipelineOptions>()
            .Bind(configuration.GetSection(CrawlPipelineOptions.SectionName));

        services.AddSingleton<IDeltaProcessor, DeltaProcessor>();
        services.AddSingleton<ICrawlQueueService, CrawlQueueService>();
        services.AddSingleton<ISourceProductBuilder, SourceProductBuilder>();

        return services;
    }
}