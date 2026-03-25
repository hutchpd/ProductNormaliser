using Microsoft.AspNetCore.Authentication;
using ProductNormaliser.Application.Categories;
using ProductNormaliser.Application.Crawls;
using ProductNormaliser.Application.Governance;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.AdminApi.Services;
using ProductNormaliser.AdminApi.OpenApi;
using ProductNormaliser.AdminApi.Security;
using ProductNormaliser.Infrastructure.Crawling;
using ProductNormaliser.Infrastructure.Mongo;
using ProductNormaliser.Infrastructure.Sources;
using ProductNormaliser.Infrastructure.StructuredData;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication(ManagementSecurityConstants.AuthenticationScheme)
    .AddScheme<AuthenticationSchemeOptions, ManagementApiKeyAuthenticationHandler>(ManagementSecurityConstants.AuthenticationScheme, _ => { });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(ManagementSecurityConstants.OperatorPolicy, policy =>
    {
        policy.AddAuthenticationSchemes(ManagementSecurityConstants.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireRole(ManagementSecurityConstants.OperatorRole);
    });
});
builder.Services.Configure<ManagementApiSecurityOptions>(builder.Configuration.GetSection(ManagementApiSecurityOptions.SectionName));
builder.Services.Configure<SourceCandidateDiscoveryOptions>(builder.Configuration.GetSection(SourceCandidateDiscoveryOptions.SectionName));
builder.Services.AddOpenApi(options =>
{
    options.AddOperationTransformer(SourceEndpointOpenApiTransformer.ApplyAsync);
});
builder.Services.AddProductNormaliserMongo(builder.Configuration);
builder.Services.AddSingleton<IStructuredDataExtractor, SchemaOrgJsonLdExtractor>();
builder.Services.AddHttpClient<IHttpFetcher, HttpFetcher>();
builder.Services.AddHttpClient<ISourceCandidateSearchProvider, SearchApiSourceCandidateSearchProvider>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SourceCandidateDiscoveryOptions>>().Value;
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
builder.Services.AddSingleton<ICategoryMetadataService, CategoryMetadataService>();
builder.Services.AddSingleton<ICategoryManagementService, CategoryManagementService>();
builder.Services.AddSingleton<ICrawlJobService, CrawlJobService>();
builder.Services.AddSingleton<ISourceCandidateProbeService, HttpSourceCandidateProbeService>();
builder.Services.AddSingleton<ISourceCandidateDiscoveryService, SourceCandidateDiscoveryService>();
builder.Services.AddSingleton<ISourceManagementService, SourceManagementService>();
builder.Services.AddSingleton<ISourceOperationalInsightsProvider, SourceOperationalInsightsProvider>();
builder.Services.AddSingleton<IAdminQueryService, AdminQueryService>();
builder.Services.AddSingleton<IDataIntelligenceService, DataIntelligenceService>();
builder.Services.AddSingleton<IAnalystWorkspaceService, AnalystWorkspaceService>();
builder.Services.AddSingleton<IManagementActorContext, HttpContextManagementActorContext>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers().RequireAuthorization(ManagementSecurityConstants.OperatorPolicy);

app.Run();
