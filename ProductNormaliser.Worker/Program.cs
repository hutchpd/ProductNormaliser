using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Merging;
using ProductNormaliser.Core.Normalisation;
using ProductNormaliser.Application.Crawls;
using ProductNormaliser.Infrastructure.Crawling;
using ProductNormaliser.Infrastructure.Discovery;
using ProductNormaliser.Infrastructure.Mongo;
using ProductNormaliser.Infrastructure.StructuredData;
using ProductNormaliser.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddProductNormaliserMongo(builder.Configuration);
builder.Services.AddSingleton<IStructuredDataExtractor, SchemaOrgJsonLdExtractor>();
builder.Services.AddSingleton<IProductIdentityResolver, ProductIdentityResolver>();
builder.Services.AddSingleton<MergeWeightCalculator>();
builder.Services.AddSingleton<ICanonicalMergeService, CanonicalMergeService>();
builder.Services.AddSingleton<IConflictDetector, ConflictDetector>();
builder.Services.AddSingleton<ICrawlJobService, CrawlJobService>();
builder.Services.AddSingleton<CrawlOrchestrator>();
builder.Services.AddSingleton<DiscoveryOrchestrator>();
builder.Services.AddHttpClient<IHttpFetcher, HttpFetcher>();
builder.Services.AddHttpClient<IRobotsPolicyService, RobotsPolicyService>();
builder.Services.AddHostedService<CrawlWorker>();
builder.Services.AddHostedService<DiscoveryWorker>();

var host = builder.Build();
host.Run();
