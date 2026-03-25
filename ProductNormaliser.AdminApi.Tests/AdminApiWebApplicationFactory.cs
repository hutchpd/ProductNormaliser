using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProductNormaliser.AdminApi.Contracts;
using ProductNormaliser.AdminApi.Services;
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
            services.RemoveAll<ISourceOperationalInsightsProvider>();
            services.AddSingleton<ISourceOperationalInsightsProvider>(new FakeSourceOperationalInsightsProvider());
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

    private sealed class FakeSourceOperationalInsightsProvider : ISourceOperationalInsightsProvider
    {
        public Task<IReadOnlyDictionary<string, SourceOperationalInsights>> BuildAsync(IReadOnlyList<CrawlSource> sources, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyDictionary<string, SourceOperationalInsights>>(sources.ToDictionary(
                source => source.Id,
                source => new SourceOperationalInsights
                {
                    Readiness = new SourceReadinessDto
                    {
                        Status = source.SupportedCategoryKeys.Count == 0 ? "Unassigned" : "Ready",
                        AssignedCategoryCount = source.SupportedCategoryKeys.Count,
                        CrawlableCategoryCount = source.SupportedCategoryKeys.Count,
                        Summary = source.SupportedCategoryKeys.Count == 0
                            ? "No categories are currently assigned."
                            : $"All {source.SupportedCategoryKeys.Count} assigned categories are crawl-ready."
                    },
                    Health = new SourceHealthSummaryDto
                    {
                        Status = "Healthy",
                        TrustScore = 90m,
                        CoveragePercent = 85m,
                        SuccessfulCrawlRate = 95m,
                        ExtractabilityRate = 82m,
                        NoProductRate = 18m,
                        SnapshotUtc = new DateTime(2026, 03, 23, 08, 00, 00, DateTimeKind.Utc)
                    },
                    LastActivity = new SourceLastActivityDto
                    {
                        TimestampUtc = new DateTime(2026, 03, 23, 09, 00, 00, DateTimeKind.Utc),
                        Status = "succeeded",
                        ExtractionOutcome = "products_extracted",
                        DurationMs = 1200,
                        ExtractedProductCount = 10,
                        HadMeaningfulChange = true,
                        MeaningfulChangeSummary = "Observed updated product content."
                    }
                },
                StringComparer.OrdinalIgnoreCase));
        }
    }
}