using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace ProductNormaliser.Web.Tests;

internal sealed class BrowserProductWebHost(FakeAdminApiClient adminApiClient) : IAsyncDisposable
{
    private WebApplication? app;

    public Uri RootUri { get; private set; } = null!;

    public async Task StartAsync()
    {
        if (app is not null)
        {
            return;
        }

        app = Program.BuildApp([], builder =>
        {
            ProductWebTestHostConfiguration.Configure(builder, adminApiClient);
            builder.WebHost.UseSetting("urls", "http://127.0.0.1:0");
        },
        environmentName: "Development",
        applicationName: typeof(Program).Assembly.GetName().Name,
        contentRootPath: GetWebContentRoot());

        await app.StartAsync();

        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
        RootUri = addresses?
            .Select(address => new Uri(address))
            .LastOrDefault()
            ?? throw new InvalidOperationException("The browser test host did not expose a base address.");
    }

    public async ValueTask DisposeAsync()
    {
        if (app is null)
        {
            return;
        }

        await app.StopAsync();
        await app.DisposeAsync();
    }

    private static string GetWebContentRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ProductNormaliser.Web"));
    }
}