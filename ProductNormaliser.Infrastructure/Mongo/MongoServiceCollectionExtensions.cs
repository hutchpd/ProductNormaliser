using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ProductNormaliser.Application.Categories;
using ProductNormaliser.Infrastructure.Crawling;
using ProductNormaliser.Infrastructure.Intelligence;
using ProductNormaliser.Infrastructure.Mongo.Repositories;
using ProductNormaliser.Infrastructure.StructuredData;
using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Normalisation;
using ProductNormaliser.Core.Schemas;

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
        services.AddSingleton<UnmappedAttributeRepository>();
        services.AddSingleton<SourceQualitySnapshotRepository>();
        services.AddSingleton<ProductChangeEventRepository>();
        services.AddSingleton<AdaptiveCrawlPolicyRepository>();
        services.AddSingleton<SourceAttributeDisagreementRepository>();
        services.AddSingleton<CrawlSourceRepository>();
        services.AddSingleton<CategoryMetadataRepository>();
        services.AddSingleton<IRawPageStore>(serviceProvider => serviceProvider.GetRequiredService<RawPageRepository>());
        services.AddSingleton<ICategoryMetadataStore>(serviceProvider => serviceProvider.GetRequiredService<CategoryMetadataRepository>());
        services.AddSingleton<ISourceProductStore>(serviceProvider => serviceProvider.GetRequiredService<SourceProductRepository>());
        services.AddSingleton<ICanonicalProductStore>(serviceProvider => serviceProvider.GetRequiredService<CanonicalProductRepository>());
        services.AddSingleton<IProductOfferStore>(serviceProvider => serviceProvider.GetRequiredService<ProductOfferRepository>());
        services.AddSingleton<IMergeConflictStore>(serviceProvider => serviceProvider.GetRequiredService<MergeConflictRepository>());
        services.AddSingleton<ICrawlQueueStore>(serviceProvider => serviceProvider.GetRequiredService<CrawlQueueRepository>());
        services.AddSingleton<ICrawlLogStore>(serviceProvider => serviceProvider.GetRequiredService<CrawlLogRepository>());
        services.AddSingleton<IUnmappedAttributeStore>(serviceProvider => serviceProvider.GetRequiredService<UnmappedAttributeRepository>());
        services.AddSingleton<IUnmappedAttributeRecorder>(serviceProvider => serviceProvider.GetRequiredService<UnmappedAttributeRepository>());
        services.AddSingleton<ISourceQualitySnapshotStore>(serviceProvider => serviceProvider.GetRequiredService<SourceQualitySnapshotRepository>());
        services.AddSingleton<IProductChangeEventStore>(serviceProvider => serviceProvider.GetRequiredService<ProductChangeEventRepository>());
        services.AddSingleton<IAdaptiveCrawlPolicyStore>(serviceProvider => serviceProvider.GetRequiredService<AdaptiveCrawlPolicyRepository>());
        services.AddSingleton<ISourceAttributeDisagreementStore>(serviceProvider => serviceProvider.GetRequiredService<SourceAttributeDisagreementRepository>());
        services.AddSingleton<ProductNormaliser.Application.Sources.ICrawlSourceStore>(serviceProvider => serviceProvider.GetRequiredService<CrawlSourceRepository>());
        services.AddSingleton<ICrawlBackoffService, AdaptiveCrawlBackoffService>();
        services.AddSingleton<ISourceDisagreementService, SourceDisagreementService>();
        services.AddSingleton<ISourceTrustService, SourceTrustService>();
        services.AddSingleton<IAttributeStabilityService, AttributeStabilityService>();
        services.AddSingleton<ICategorySchemaProvider, TvCategorySchemaProvider>();
        services.AddSingleton<ICategorySchemaProvider, MonitorCategorySchemaProvider>();
        services.AddSingleton<ICategorySchemaProvider, LaptopCategorySchemaProvider>();
        services.AddSingleton<ICategorySchemaProvider, RefrigeratorCategorySchemaProvider>();
        services.AddSingleton<ICategorySchemaRegistry, CategorySchemaRegistry>();
        services.AddSingleton<ICategoryAttributeNormaliser, TvAttributeNormaliser>();
        services.AddSingleton<ICategoryAttributeNormaliser, MonitorAttributeNormaliser>();
        services.AddSingleton<ICategoryAttributeNormaliser, LaptopAttributeNormaliser>();
        services.AddSingleton<ICategoryAttributeNormaliser, RefrigeratorAttributeNormaliser>();
        services.AddSingleton<CategoryAttributeNormaliserRegistry>();
        services.AddSingleton<ICategoryAttributeNormaliserRegistry>(serviceProvider => serviceProvider.GetRequiredService<CategoryAttributeNormaliserRegistry>());
        services.AddSingleton<IAttributeNormaliser>(serviceProvider => serviceProvider.GetRequiredService<CategoryAttributeNormaliserRegistry>());

        services.AddOptions<CrawlPipelineOptions>()
            .Bind(configuration.GetSection(CrawlPipelineOptions.SectionName));

        services.AddSingleton<IDeltaProcessor, DeltaProcessor>();
        services.AddSingleton<ICrawlPriorityService, CrawlPriorityService>();
        services.AddSingleton<ICrawlQueueService, CrawlQueueService>();
        services.AddSingleton<ISourceProductBuilder, SourceProductBuilder>();

        return services;
    }
}