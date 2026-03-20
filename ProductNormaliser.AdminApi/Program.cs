using ProductNormaliser.AdminApi.Services;
using ProductNormaliser.Infrastructure.Mongo;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProductNormaliserMongo(builder.Configuration);
builder.Services.AddSingleton<IAdminQueryService, AdminQueryService>();

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
