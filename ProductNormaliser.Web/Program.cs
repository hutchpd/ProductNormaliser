using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using ProductNormaliser.Web.Options;
using ProductNormaliser.Web.Security;
using ProductNormaliser.Web.Services;

var app = Program.BuildApp(args);
app.Run();

public partial class Program
{
    public static WebApplication BuildApp(
        string[] args,
        Action<WebApplicationBuilder>? configureBuilder = null,
        string? environmentName = null,
        string? applicationName = null,
        string? contentRootPath = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            EnvironmentName = environmentName,
            ApplicationName = applicationName,
            ContentRootPath = contentRootPath
        });

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
            options.Conventions.AllowAnonymousToPage("/Privacy");
            options.Conventions.AllowAnonymousToPage("/SetupRequired");
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
            client.Timeout = TimeSpan.FromMinutes(4);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }).AddHttpMessageHandler<AdminApiManagementHeadersHandler>();

        configureBuilder?.Invoke(builder);

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/SetupRequired", StringComparison.OrdinalIgnoreCase)
                || context.Request.Path.StartsWithSegments(ManagementWebSecurityConstants.LoginPath, StringComparison.OrdinalIgnoreCase)
                || context.Request.Path.StartsWithSegments("/Privacy", StringComparison.OrdinalIgnoreCase)
                || context.Request.Path.StartsWithSegments("/Forbidden", StringComparison.OrdinalIgnoreCase)
                || context.Request.Path.StartsWithSegments("/Error", StringComparison.OrdinalIgnoreCase)
                || context.Request.Path.StartsWithSegments("/css", StringComparison.OrdinalIgnoreCase)
                || context.Request.Path.StartsWithSegments("/js", StringComparison.OrdinalIgnoreCase)
                || context.Request.Path.StartsWithSegments("/lib", StringComparison.OrdinalIgnoreCase)
                || context.Request.Path.StartsWithSegments("/_framework", StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            if (context.User.Identity?.IsAuthenticated == true)
            {
                try
                {
                    var client = context.RequestServices.GetRequiredService<IProductNormaliserAdminApiClient>();
                    await client.GetStatsAsync(context.RequestAborted);
                }
                catch
                {
                    context.Response.Redirect("/SetupRequired");
                    return;
                }
            }

            await next();
        });

        app.MapStaticAssets();
        app.MapRazorPages()
           .WithStaticAssets();

        return app;
    }
}
