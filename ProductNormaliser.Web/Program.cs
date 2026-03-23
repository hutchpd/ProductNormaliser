using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using ProductNormaliser.Web.Options;
using ProductNormaliser.Web.Security;
using ProductNormaliser.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<AdminApiManagementHeadersHandler>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = ManagementWebSecurityConstants.LoginPath;
        options.AccessDeniedPath = ManagementWebSecurityConstants.AccessDeniedPath;
        options.Cookie.Name = builder.Configuration[$"{ManagementWebSecurityOptions.SectionName}:CookieName"] ?? ".ProductNormaliser.Management";
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(ManagementWebSecurityConstants.OperatorPolicy, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole(ManagementWebSecurityConstants.OperatorRole);
    });
});
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/", ManagementWebSecurityConstants.OperatorPolicy);
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/Forbidden");
});
builder.Services.Configure<AdminApiOptions>(builder.Configuration.GetSection(AdminApiOptions.SectionName));
builder.Services.Configure<ManagementWebSecurityOptions>(builder.Configuration.GetSection(ManagementWebSecurityOptions.SectionName));
builder.Services.AddSingleton<IManagementUserValidator, ManagementUserValidator>();
builder.Services.AddHttpClient<IProductNormaliserAdminApiClient, ProductNormaliserAdminApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<AdminApiOptions>>().Value;
    if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseAddress))
    {
        throw new InvalidOperationException($"Configuration value '{AdminApiOptions.SectionName}:BaseUrl' must be an absolute URI.");
    }

    client.BaseAddress = baseAddress;
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
}).AddHttpMessageHandler<AdminApiManagementHeadersHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
