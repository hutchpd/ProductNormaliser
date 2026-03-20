using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Merging;
using ProductNormaliser.Core.Normalisation;
using ProductNormaliser.Infrastructure.Crawling;
using ProductNormaliser.Infrastructure.Mongo;
using ProductNormaliser.Infrastructure.StructuredData;
using ProductNormaliser.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddProductNormaliserMongo(builder.Configuration);
builder.Services.AddSingleton<IStructuredDataExtractor, SchemaOrgJsonLdExtractor>();
builder.Services.AddSingleton<IAttributeNormaliser, TvAttributeNormaliser>();
builder.Services.AddSingleton<IProductIdentityResolver, ProductIdentityResolver>();
builder.Services.AddSingleton<ICanonicalMergeService, CanonicalMergeService>();
builder.Services.AddSingleton<IConflictDetector, ConflictDetector>();
builder.Services.AddSingleton<CrawlOrchestrator>();
builder.Services.AddHttpClient<IHttpFetcher, HttpFetcher>();
builder.Services.AddHttpClient<IRobotsPolicyService, RobotsPolicyService>();
builder.Services.AddHostedService<CrawlWorker>();

var host = builder.Build();
host.Run();
