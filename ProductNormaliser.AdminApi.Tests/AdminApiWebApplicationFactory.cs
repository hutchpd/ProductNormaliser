using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.AdminApi.Tests;

internal sealed class AdminApiWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ManagementApiSecurity:ApiKeys:0:KeyId"] = "operator-key",
                ["ManagementApiSecurity:ApiKeys:0:Secret"] = "operator-secret",
                ["ManagementApiSecurity:ApiKeys:0:Role"] = "operator",
                ["ManagementApiSecurity:ApiKeys:1:KeyId"] = "viewer-key",
                ["ManagementApiSecurity:ApiKeys:1:Secret"] = "viewer-secret",
                ["ManagementApiSecurity:ApiKeys:1:Role"] = "viewer"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ISourceManagementService>();
            services.AddSingleton<ISourceManagementService>(new FakeSourceManagementService());
        });
    }

    private sealed class FakeSourceManagementService : ISourceManagementService
    {
        public Task<IReadOnlyList<CrawlSource>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<CrawlSource>>(
            [
                new CrawlSource
                {
                    Id = "ao_uk",
                    DisplayName = "AO UK",
                    BaseUrl = "https://ao.example",
                    Host = "ao.example",
                    IsEnabled = true,
                    SupportedCategoryKeys = ["tv"],
                    ThrottlingPolicy = new SourceThrottlingPolicy(),
                    CreatedUtc = DateTime.UtcNow,
                    UpdatedUtc = DateTime.UtcNow
                }
            ]);
        }

        public Task<CrawlSource?> GetAsync(string sourceId, CancellationToken cancellationToken = default)
            => Task.FromResult<CrawlSource?>(null);

        public Task<CrawlSource> RegisterAsync(CrawlSourceRegistration registration, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<CrawlSource> UpdateAsync(string sourceId, CrawlSourceUpdate update, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<CrawlSource> EnableAsync(string sourceId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<CrawlSource> DisableAsync(string sourceId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<CrawlSource> AssignCategoriesAsync(string sourceId, IReadOnlyCollection<string> categoryKeys, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<CrawlSource> SetThrottlingAsync(string sourceId, SourceThrottlingPolicy policy, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}