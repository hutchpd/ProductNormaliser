using Microsoft.Extensions.Options;
using ProductNormaliser.Application.AI;
using ProductNormaliser.Application.Categories;
using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Merging;
using ProductNormaliser.Core.Normalisation;
using ProductNormaliser.Application.Crawls;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Infrastructure.Crawling;
using ProductNormaliser.Infrastructure.AI;
using ProductNormaliser.Infrastructure.Discovery;
using ProductNormaliser.Infrastructure.Mongo;
using ProductNormaliser.Infrastructure.Sources;
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
builder.Services.Configure<SourceCandidateDiscoveryOptions>(builder.Configuration.GetSection(SourceCandidateDiscoveryOptions.SectionName));
builder.Services.Configure<SourceOnboardingAutomationOptions>(builder.Configuration.GetSection(SourceOnboardingAutomationOptions.SectionName));
builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection(LlmOptions.SectionName));
builder.Services.AddSingleton<CrawlOrchestrator>();
builder.Services.AddSingleton<DiscoveryOrchestrator>();
builder.Services.AddSingleton<ICategoryMetadataService, CategoryMetadataService>();
builder.Services.AddSingleton<ISourceManagementService, SourceManagementService>();
builder.Services.AddSingleton<LlamaPageClassificationService>(serviceProvider =>
{
	var llmOptions = serviceProvider.GetRequiredService<IOptions<LlmOptions>>().Value;
	var hostEnvironment = serviceProvider.GetRequiredService<IHostEnvironment>();
	var logger = serviceProvider.GetRequiredService<ILogger<LlamaPageClassificationService>>();
	return new LlamaPageClassificationService(llmOptions, hostEnvironment.ContentRootPath, logger);
});
builder.Services.AddSingleton<IPageClassificationService>(serviceProvider => serviceProvider.GetRequiredService<LlamaPageClassificationService>());
builder.Services.AddSingleton<ILlmStatusProvider>(serviceProvider => serviceProvider.GetRequiredService<LlamaPageClassificationService>());
builder.Services.AddSingleton<ISourceCandidateProbeService, HttpSourceCandidateProbeService>();
builder.Services.AddSingleton<IDiscoveryRunProcessor, DiscoveryRunProcessor>();
builder.Services.AddHttpClient<ISourceCandidateSearchProvider, SearchApiSourceCandidateSearchProvider>((serviceProvider, client) =>
{
	var options = serviceProvider.GetRequiredService<IOptions<SourceCandidateDiscoveryOptions>>().Value;
	if (Uri.TryCreate(options.SearchApiBaseUrl, UriKind.Absolute, out var baseAddress))
	{
		client.BaseAddress = baseAddress;
	}

	if (!string.IsNullOrWhiteSpace(options.SearchApiKey))
	{
		client.DefaultRequestHeaders.Remove("X-Subscription-Token");
		client.DefaultRequestHeaders.TryAddWithoutValidation("X-Subscription-Token", options.SearchApiKey);
	}
});
builder.Services.AddHttpClient<IHttpFetcher, HttpFetcher>();
builder.Services.AddHttpClient<IRobotsPolicyService, RobotsPolicyService>();
builder.Services.AddHostedService<CrawlWorker>();
builder.Services.AddHostedService<DiscoveryWorker>();
builder.Services.AddHostedService<DiscoveryRunWorker>();

var host = builder.Build();
host.Run();
