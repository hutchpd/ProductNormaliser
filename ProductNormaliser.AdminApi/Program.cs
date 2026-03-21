using ProductNormaliser.Application.Categories;
using ProductNormaliser.Application.Crawls;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.AdminApi.Services;
using ProductNormaliser.AdminApi.OpenApi;
using ProductNormaliser.Infrastructure.Mongo;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    options.AddOperationTransformer(SourceEndpointOpenApiTransformer.ApplyAsync);
});
builder.Services.AddProductNormaliserMongo(builder.Configuration);
builder.Services.AddSingleton<ICategoryMetadataService, CategoryMetadataService>();
builder.Services.AddSingleton<ICategoryManagementService, CategoryManagementService>();
builder.Services.AddSingleton<ICrawlJobService, CrawlJobService>();
builder.Services.AddSingleton<ISourceManagementService, SourceManagementService>();
builder.Services.AddSingleton<IAdminQueryService, AdminQueryService>();
builder.Services.AddSingleton<IDataIntelligenceService, DataIntelligenceService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
