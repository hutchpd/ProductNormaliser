using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProductNormaliser.Application.AI;
using ProductNormaliser.Application.Categories;
using ProductNormaliser.Application.Governance;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.AI;
using ProductNormaliser.Infrastructure.Crawling;
using ProductNormaliser.Infrastructure.Sources;
using ProductNormaliser.Infrastructure.StructuredData;
using ProductNormaliser.Worker;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.Discovery)]
public sealed class DiscoveryRunRegressionHarnessTests
{
    [Test]
    public void AddWorkerDiscoveryServices_RegistersDiscoveryRunMaintenanceDependencies()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDiscoveryRunStore, InMemoryDiscoveryRunStore>();
        services.AddSingleton<IDiscoveryCampaignStore, InMemoryDiscoveryCampaignStore>();
        services.AddSingleton<IDiscoveryRunCandidateStore, InMemoryDiscoveryRunCandidateStore>();
        services.AddSingleton<IDiscoveryRunCandidateDispositionStore, InMemoryDiscoveryRunCandidateDispositionStore>();
        services.AddSingleton<ICrawlSourceStore, InMemoryCrawlSourceStore>();
        services.AddSingleton<ICategoryMetadataService>(new InMemoryCategoryMetadataService("tv"));
        services.AddSingleton<ISourceManagementService, RecordingSourceManagementService>();
        services.AddSingleton<IManagementAuditService, NoOpManagementAuditService>();
        services.AddSingleton<ICrawlGovernanceService, AllowAllCrawlGovernanceService>();
        services.AddSingleton<ISourceCandidateSearchProvider, NoOpSourceCandidateSearchProvider>();
        services.AddSingleton<ISourceCandidateProbeService>(new FixedProbeService(CreateStrongProbeResult()));
        services.AddSingleton<IOptions<SourceOnboardingAutomationOptions>>(Options.Create(new SourceOnboardingAutomationOptions()));
        services.AddSingleton<IOptions<DiscoveryRunOperationsOptions>>(Options.Create(new DiscoveryRunOperationsOptions()));
        services.AddWorkerDiscoveryServices();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.Multiple(() =>
        {
            Assert.That(provider.GetRequiredService<IDiscoveryRunService>(), Is.Not.Null);
            Assert.That(provider.GetRequiredService<IDiscoveryRunProcessor>(), Is.Not.Null);
            Assert.That(provider.GetRequiredService<DiscoveryRunMaintenanceService>(), Is.Not.Null);
        });
    }

    [Test]
    [Explicit("Live 10-minute discovery polling harness")]
    [CancelAfter(720000)]
    public async Task DiscoveryRun_TvPollingHarness_CompletesAndPersistsSearchProgress()
    {
        var observationWindow = TimeSpan.FromMinutes(10);
        var harnessStopwatch = Stopwatch.StartNew();
        var liveConfiguration = LoadLiveHarnessConfiguration();
        var liveSearchOptions = BindLiveSearchOptions(liveConfiguration);
        if (string.IsNullOrWhiteSpace(liveSearchOptions.SearchApiKey))
        {
            Assert.Ignore("SourceCandidateDiscovery:SearchApiKey is not configured for the live discovery harness.");
            return;
        }

        var runStore = new InMemoryDiscoveryRunStore();
        var candidateStore = new InMemoryDiscoveryRunCandidateStore();
        var dispositionStore = new InMemoryDiscoveryRunCandidateDispositionStore();
        var categoryService = new InMemoryCategoryMetadataService("tv");
        var sourceManagementService = new RecordingSourceManagementService();
        var managementAuditService = new NoOpManagementAuditService();
        var crawlSourceStore = new InMemoryCrawlSourceStore();
        using var searchHttpClient = new HttpClient();
        using var fetchHttpClient = new HttpClient();
        var discoveryRunOperationsOptions = new DiscoveryRunOperationsOptions
        {
            SearchTimeoutSeconds = Math.Max(30, liveSearchOptions.SearchTimeoutSeconds),
            ProbeTimeoutSeconds = Math.Max(20, liveSearchOptions.ProbeTimeoutSeconds),
            LlmVerificationTimeoutMs = 2000
        };
        var searchProvider = new SearchApiSourceCandidateSearchProvider(
            searchHttpClient,
            Options.Create(liveSearchOptions),
            Options.Create(discoveryRunOperationsOptions));
        var probeService = new HttpSourceCandidateProbeService(
            new HttpFetcher(fetchHttpClient, Options.Create(new CrawlPipelineOptions()), crawlSourceStore),
            new SchemaOrgJsonLdExtractor(),
            new UnusedPageClassificationService(),
            Options.Create(liveSearchOptions),
            Options.Create(new SourceOnboardingAutomationOptions()),
            Options.Create(discoveryRunOperationsOptions),
            Options.Create(new LlmOptions { Enabled = false }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<HttpSourceCandidateProbeService>.Instance);
        var runService = new DiscoveryRunService(
            runStore,
            candidateStore,
            dispositionStore,
            categoryService,
            sourceManagementService,
            managementAuditService);
        var processor = new DiscoveryRunProcessor(
            runStore,
            candidateStore,
            dispositionStore,
            crawlSourceStore,
            sourceManagementService,
            new AllowAllCrawlGovernanceService(),
            searchProvider,
            probeService,
            Options.Create(new SourceOnboardingAutomationOptions()),
            Options.Create(discoveryRunOperationsOptions));

        var run = await runService.CreateAsync(new CreateDiscoveryRunRequest
        {
            CategoryKeys = ["tv"],
            Market = "UK",
            Locale = "en-GB",
            AutomationMode = SourceAutomationModes.OperatorAssisted,
            MaxCandidates = 5
        }, CancellationToken.None);

        var processingTask = processor.ProcessNextAsync(CancellationToken.None);
        var deadlineUtc = DateTime.UtcNow.Add(observationWindow);
        var observedRunning = false;
        var observedSearchDiagnostics = false;
        var observedSearchResults = false;
        var observedCandidates = false;
        var observedTerminalCompletion = false;
        var lastLoggedSnapshotUtc = DateTime.MinValue;
        DiscoveryRun? finalRun = null;

        while (DateTime.UtcNow < deadlineUtc)
        {
            finalRun = await runStore.GetAsync(run.RunId, CancellationToken.None);
            var candidates = await candidateStore.ListByRunAsync(run.RunId, CancellationToken.None);
            if (finalRun is not null)
            {
                observedRunning |= string.Equals(finalRun.Status, DiscoveryRunStatuses.Running, StringComparison.OrdinalIgnoreCase);
                observedSearchDiagnostics |= finalRun.Diagnostics.Any(diagnostic =>
                    diagnostic.Code.StartsWith("search_query_started_", StringComparison.OrdinalIgnoreCase)
                    || diagnostic.Code.StartsWith("search_query_results_", StringComparison.OrdinalIgnoreCase));
                observedSearchResults |= finalRun.SearchResultCount > 0;
                observedTerminalCompletion |= string.Equals(finalRun.Status, DiscoveryRunStatuses.Completed, StringComparison.OrdinalIgnoreCase);

                if (lastLoggedSnapshotUtc == DateTime.MinValue || DateTime.UtcNow - lastLoggedSnapshotUtc >= TimeSpan.FromSeconds(30))
                {
                    lastLoggedSnapshotUtc = DateTime.UtcNow;
                    TestContext.Progress.WriteLine($"[{DateTime.UtcNow:u}] status={finalRun.Status} stage={finalRun.CurrentStage} searchResults={finalRun.SearchResultCount} collapsed={finalRun.CollapsedCandidateCount} candidates={candidates.Count} diagnostics={finalRun.Diagnostics.Count}");
                }
            }

            observedCandidates |= candidates.Count > 0;

            if (finalRun is not null
                && IsTerminalStatus(finalRun.Status)
                && !string.Equals(finalRun.Status, DiscoveryRunStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None);
        }

        await processingTask;
        finalRun ??= await runStore.GetAsync(run.RunId, CancellationToken.None);
        var finalCandidates = await candidateStore.ListByRunAsync(run.RunId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(finalRun, Is.Not.Null);
            Assert.That(harnessStopwatch.Elapsed, Is.GreaterThanOrEqualTo(observationWindow), "The live harness must observe the run for the full ten-minute window.");
            Assert.That(observedRunning, Is.True, "The run never left the queued state while being polled.");
            Assert.That(observedSearchDiagnostics, Is.True, "No live search-query diagnostics were persisted while the run was executing.");
            Assert.That(observedSearchResults, Is.True, "The run never reported any search results while being polled.");
            Assert.That(observedCandidates, Is.True, "No candidates were ever materialized for the run.");
            Assert.That(observedTerminalCompletion, Is.True, "The run did not complete successfully during the ten-minute observation window.");
            Assert.That(finalRun!.Status, Is.EqualTo(DiscoveryRunStatuses.Completed));
            Assert.That(finalRun.SearchResultCount, Is.GreaterThan(0));
            Assert.That(finalRun.CollapsedCandidateCount, Is.GreaterThan(0));
            Assert.That(finalCandidates.Count, Is.GreaterThan(0));
        });
    }

    private static SourceCandidateProbeResult CreateStrongProbeResult()
    {
        return new SourceCandidateProbeResult
        {
            HomePageReachable = true,
            RobotsTxtReachable = true,
            SitemapDetected = true,
            CrawlabilityScore = 94m,
            CategoryRelevanceScore = 92m,
            ExtractabilityScore = 91m,
            CatalogLikelihoodScore = 93m,
            RepresentativeCategoryPageUrl = "https://panel.example/tv",
            RepresentativeCategoryPageReachable = true,
            RepresentativeProductPageUrl = "https://panel.example/tv-1",
            RepresentativeProductPageReachable = true,
            RuntimeExtractionCompatible = true,
            RepresentativeRuntimeProductCount = 4,
            AutomationCategorySampleCount = 3,
            AutomationReachableCategorySampleCount = 3,
            AutomationProductSampleCount = 3,
            AutomationReachableProductSampleCount = 3,
            AutomationRuntimeCompatibleProductSampleCount = 3,
            AutomationStructuredProductEvidenceSampleCount = 3,
            StructuredProductEvidenceDetected = true,
            TechnicalAttributeEvidenceDetected = true,
            LlmAcceptedRepresentativeProductPage = true,
            LlmConfidenceScore = 95m,
            ProbeElapsedMs = 120,
            LlmElapsedMs = 300,
            LlmBudgetMs = 5000
        };
    }

    private static bool IsTerminalStatus(string status)
    {
        return string.Equals(status, DiscoveryRunStatuses.Completed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, DiscoveryRunStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, DiscoveryRunStatuses.Failed, StringComparison.OrdinalIgnoreCase);
    }

    private static IConfigurationRoot LoadLiveHarnessConfiguration()
    {
        return new ConfigurationBuilder()
            .AddUserSecrets(typeof(WorkerDiscoveryServiceCollectionExtensions).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static SourceCandidateDiscoveryOptions BindLiveSearchOptions(IConfiguration configuration)
    {
        var options = new SourceCandidateDiscoveryOptions();
        configuration.GetSection(SourceCandidateDiscoveryOptions.SectionName).Bind(options);
        return options;
    }

    private sealed class InMemoryDiscoveryRunStore : IDiscoveryRunStore
    {
        private readonly Dictionary<string, DiscoveryRun> runs = new(StringComparer.OrdinalIgnoreCase);

        public Task<DiscoveryRunPage> ListAsync(DiscoveryRunQuery query, CancellationToken cancellationToken = default)
        {
            var items = runs.Values
                .Where(run => string.IsNullOrWhiteSpace(query.Status) || string.Equals(run.Status, query.Status, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(run => run.UpdatedUtc)
                .ToArray();

            return Task.FromResult(new DiscoveryRunPage
            {
                Items = items,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = items.Length
            });
        }

        public Task<DiscoveryRun?> GetAsync(string runId, CancellationToken cancellationToken = default)
        {
            runs.TryGetValue(runId, out var run);
            return Task.FromResult(run);
        }

        public Task<DiscoveryRun?> GetNextQueuedAsync(CancellationToken cancellationToken = default)
        {
            var run = runs.Values
                .Where(item => string.Equals(item.Status, DiscoveryRunStatuses.Queued, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.CreatedUtc)
                .FirstOrDefault();
            return Task.FromResult(run);
        }

        public Task<IReadOnlyList<DiscoveryRun>> ListByStatusesAsync(IReadOnlyCollection<string> statuses, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DiscoveryRun>>(runs.Values.Where(run => statuses.Contains(run.Status)).ToArray());
        }

        public Task<IReadOnlyList<DiscoveryRun>> ListByCampaignAsync(string campaignId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DiscoveryRun>>(runs.Values.Where(run => string.Equals(run.RecurringCampaignId, campaignId, StringComparison.OrdinalIgnoreCase)).ToArray());
        }

        public Task<bool> HasIncompleteCampaignRunAsync(string campaignId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(runs.Values.Any(run => string.Equals(run.RecurringCampaignId, campaignId, StringComparison.OrdinalIgnoreCase)
                && !IsTerminalStatus(run.Status)));
        }

        public Task UpsertAsync(DiscoveryRun run, CancellationToken cancellationToken = default)
        {
            runs[run.RunId] = run;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryDiscoveryRunCandidateStore : IDiscoveryRunCandidateStore
    {
        private readonly Dictionary<string, DiscoveryRunCandidate> candidates = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<DiscoveryRunCandidate>> ListByRunAsync(string runId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DiscoveryRunCandidate>>(candidates.Values.Where(candidate => string.Equals(candidate.RunId, runId, StringComparison.OrdinalIgnoreCase)).OrderBy(candidate => candidate.CreatedUtc).ToArray());
        }

        public Task<IReadOnlyList<DiscoveryRunCandidate>> ListByHostsAsync(IReadOnlyCollection<string> hosts, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DiscoveryRunCandidate>>(candidates.Values.Where(candidate => hosts.Contains(candidate.Host, StringComparer.OrdinalIgnoreCase)).ToArray());
        }

        public async Task<DiscoveryRunCandidatePage> QueryByRunAsync(string runId, DiscoveryRunCandidateQuery query, CancellationToken cancellationToken = default)
        {
            var items = (await ListByRunAsync(runId, cancellationToken)).AsEnumerable();
            items = query.StateFilter switch
            {
                DiscoveryRunCandidateStateFilters.Active => items.Where(candidate => !IsArchivedState(candidate.State)),
                DiscoveryRunCandidateStateFilters.Archived => items.Where(candidate => IsArchivedState(candidate.State)),
                _ => items
            };

            var array = items.ToArray();
            return new DiscoveryRunCandidatePage
            {
                Items = array,
                StateFilter = query.StateFilter ?? DiscoveryRunCandidateStateFilters.All,
                Sort = query.Sort ?? DiscoveryRunCandidateSortModes.ReviewPriority,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = array.Length,
                Summary = new DiscoveryRunCandidateRunSummary
                {
                    RunCandidateCount = candidates.Values.Count(candidate => string.Equals(candidate.RunId, runId, StringComparison.OrdinalIgnoreCase)),
                    ActiveCandidateCount = candidates.Values.Count(candidate => string.Equals(candidate.RunId, runId, StringComparison.OrdinalIgnoreCase) && !IsArchivedState(candidate.State)),
                    ArchivedCandidateCount = candidates.Values.Count(candidate => string.Equals(candidate.RunId, runId, StringComparison.OrdinalIgnoreCase) && IsArchivedState(candidate.State))
                }
            };
        }

        public Task<DiscoveryRunCandidate?> GetAsync(string runId, string candidateKey, CancellationToken cancellationToken = default)
        {
            candidates.TryGetValue(BuildKey(runId, candidateKey), out var candidate);
            return Task.FromResult(candidate);
        }

        public Task UpsertAsync(DiscoveryRunCandidate candidate, CancellationToken cancellationToken = default)
        {
            candidates[BuildKey(candidate.RunId, candidate.CandidateKey)] = candidate;
            return Task.CompletedTask;
        }

        public Task<bool> TryUpdateAsync(DiscoveryRunCandidate candidate, int expectedRevision, CancellationToken cancellationToken = default)
        {
            var key = BuildKey(candidate.RunId, candidate.CandidateKey);
            if (!candidates.TryGetValue(key, out var existing) || existing.Revision != expectedRevision)
            {
                return Task.FromResult(false);
            }

            candidates[key] = candidate;
            return Task.FromResult(true);
        }

        private static bool IsArchivedState(string state)
        {
            return string.Equals(state, DiscoveryRunCandidateStates.Archived, StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, DiscoveryRunCandidateStates.Dismissed, StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, DiscoveryRunCandidateStates.Superseded, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildKey(string runId, string candidateKey) => $"{runId}:{candidateKey}";
    }

    private sealed class InMemoryDiscoveryCampaignStore : IDiscoveryCampaignStore
    {
        public Task<IReadOnlyList<RecurringDiscoveryCampaign>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<RecurringDiscoveryCampaign>>([]);
        }

        public Task<IReadOnlyList<RecurringDiscoveryCampaign>> ListByStatusesAsync(IReadOnlyCollection<string> statuses, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<RecurringDiscoveryCampaign>>([]);
        }

        public Task<IReadOnlyList<RecurringDiscoveryCampaign>> ListDueAsync(DateTime utcNow, int limit, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<RecurringDiscoveryCampaign>>([]);
        }

        public Task<RecurringDiscoveryCampaign?> GetAsync(string campaignId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<RecurringDiscoveryCampaign?>(null);
        }

        public Task<RecurringDiscoveryCampaign?> GetByFingerprintAsync(string campaignFingerprint, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<RecurringDiscoveryCampaign?>(null);
        }

        public Task UpsertAsync(RecurringDiscoveryCampaign campaign, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(string campaignId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }
    }

    private sealed class InMemoryDiscoveryRunCandidateDispositionStore : IDiscoveryRunCandidateDispositionStore
    {
        public Task<IReadOnlyList<DiscoveryRunCandidateDisposition>> FindActiveMatchesAsync(string scopeFingerprint, string normalizedHost, string normalizedBaseUrl, string normalizedDisplayName, IReadOnlyCollection<string> allowedMarkets, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DiscoveryRunCandidateDisposition>>([]);
        }

        public Task UpsertAsync(DiscoveryRunCandidateDisposition disposition, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryCrawlSourceStore : ICrawlSourceStore
    {
        public Task<IReadOnlyList<CrawlSource>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<CrawlSource>>([]);
        }

        public Task<CrawlSource?> GetAsync(string sourceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<CrawlSource?>(null);
        }

        public Task UpsertAsync(CrawlSource source, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryCategoryMetadataService(string categoryKey) : ICategoryMetadataService
    {
        private readonly CategoryMetadata category = new()
        {
            CategoryKey = categoryKey,
            DisplayName = "TVs",
            FamilyKey = "electronics",
            FamilyDisplayName = "Electronics",
            IconKey = "tv",
            IsEnabled = true
        };

        public Task EnsureDefaultsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<CategoryMetadata>> ListAsync(bool enabledOnly = false, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<CategoryMetadata> result = enabledOnly && !category.IsEnabled ? [] : [category];
            return Task.FromResult(result);
        }

        public Task<CategoryMetadata?> GetAsync(string categoryKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(string.Equals(category.CategoryKey, categoryKey, StringComparison.OrdinalIgnoreCase) ? category : null);
        }

        public Task<CategoryMetadata> UpsertAsync(CategoryMetadata categoryMetadata, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(categoryMetadata);
        }
    }

    private sealed class RecordingSourceManagementService : ISourceManagementService
    {
        public Task<IReadOnlyList<CrawlSource>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<CrawlSource>>([]);
        }

        public Task<CrawlSource?> GetAsync(string sourceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<CrawlSource?>(null);
        }

        public Task<CrawlSource> RegisterAsync(CrawlSourceRegistration registration, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CrawlSource
            {
                Id = registration.SourceId,
                DisplayName = registration.DisplayName,
                BaseUrl = registration.BaseUrl,
                Host = new Uri(registration.BaseUrl, UriKind.Absolute).Host,
                AllowedMarkets = registration.AllowedMarkets.ToList(),
                PreferredLocale = registration.PreferredLocale ?? "en-GB",
                SupportedCategoryKeys = registration.SupportedCategoryKeys.ToList(),
                AutomationPolicy = registration.AutomationPolicy ?? new SourceAutomationPolicy(),
                IsEnabled = true,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            });
        }

        public Task<CrawlSource> UpdateAsync(string sourceId, CrawlSourceUpdate update, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CrawlSource> EnableAsync(string sourceId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CrawlSource> DisableAsync(string sourceId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CrawlSource> AssignCategoriesAsync(string sourceId, IReadOnlyCollection<string> categoryKeys, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CrawlSource> SetThrottlingAsync(string sourceId, SourceThrottlingPolicy policy, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class NoOpManagementAuditService : IManagementAuditService
    {
        public Task RecordAsync(string action, string targetType, string targetId, IReadOnlyDictionary<string, string>? details = null, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ManagementAuditEntry>> ListRecentAsync(int take = 100, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ManagementAuditEntry>>([]);
        }
    }

    private sealed class AllowAllCrawlGovernanceService : ICrawlGovernanceService
    {
        public void ValidateSourceBaseUrl(string baseUrl, string parameterName)
        {
        }

        public void ValidateCrawlRequest(string requestType, IReadOnlyCollection<string> categories, IReadOnlyCollection<string> sources, IReadOnlyCollection<string> productIds, IReadOnlyCollection<ProductNormaliser.Application.Crawls.CrawlJobTargetDescriptor> targets, string parameterName)
        {
        }
    }

    private sealed class FixedProbeService(SourceCandidateProbeResult result) : ISourceCandidateProbeService
    {
        public Task<SourceCandidateProbeResult> ProbeAsync(SourceCandidateSearchResult candidate, IReadOnlyCollection<string> categoryKeys, string automationMode, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(result);
        }
    }

    private sealed class UnusedPageClassificationService : IPageClassificationService
    {
        public Task<PageClassificationResult> ClassifyAsync(string content, string category, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Live discovery harness should run with LLM disabled and must not invoke page classification.");
        }
    }

    private sealed class SlowProgressSearchProvider : IProgressReportingSourceCandidateSearchProvider
    {
        public Task<SourceCandidateSearchResponse> SearchAsync(DiscoverSourceCandidatesRequest request, CancellationToken cancellationToken = default)
        {
            return SearchAsync(request, static (_, _) => Task.CompletedTask, cancellationToken);
        }

        public async Task<SourceCandidateSearchResponse> SearchAsync(DiscoverSourceCandidatesRequest request, Func<SourceCandidateDiscoveryDiagnostic, CancellationToken, Task> progressReporter, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(progressReporter);

            var queries = new[]
            {
                "tv retailer UK en-GB",
                "buy tv online UK en-GB"
            };

            for (var index = 0; index < queries.Length; index++)
            {
                await progressReporter(new SourceCandidateDiscoveryDiagnostic
                {
                    RecordedUtc = DateTime.UtcNow,
                    Code = $"search_query_started_{index + 1:D3}",
                    Severity = SourceCandidateDiscoveryDiagnostic.SeverityInfo,
                    Title = $"Brave query {index + 1}/{queries.Length} started",
                    Message = $"Searching Brave for \"{queries[index]}\"."
                }, cancellationToken);

                await Task.Delay(300, cancellationToken);

                await progressReporter(new SourceCandidateDiscoveryDiagnostic
                {
                    RecordedUtc = DateTime.UtcNow,
                    Code = $"search_query_results_{index + 1:D3}",
                    Severity = SourceCandidateDiscoveryDiagnostic.SeverityInfo,
                    Title = $"Brave query {index + 1}/{queries.Length} returned 2 candidate(s)",
                    Message = $"Query \"{queries[index]}\" returned 2 candidate(s): Panel Store <panel.example>, Vision Direct <vision.example>"
                }, cancellationToken);
            }

            return new SourceCandidateSearchResponse
            {
                Candidates =
                [
                    new SourceCandidateSearchResult
                    {
                        CandidateKey = "panel_example",
                        DisplayName = "Panel Store",
                        BaseUrl = "https://panel.example/",
                        Host = "panel.example",
                        CandidateType = "retailer",
                        AllowedMarkets = ["UK"],
                        PreferredLocale = "en-GB",
                        MarketEvidence = "market_path",
                        LocaleEvidence = "html_lang",
                        MatchedCategoryKeys = request.CategoryKeys.ToArray(),
                        SearchReasons = ["Strong category relevance"]
                    },
                    new SourceCandidateSearchResult
                    {
                        CandidateKey = "vision_example",
                        DisplayName = "Vision Direct",
                        BaseUrl = "https://vision.example/",
                        Host = "vision.example",
                        CandidateType = "retailer",
                        AllowedMarkets = ["UK"],
                        PreferredLocale = "en-GB",
                        MarketEvidence = "market_path",
                        LocaleEvidence = "html_lang",
                        MatchedCategoryKeys = request.CategoryKeys.ToArray(),
                        SearchReasons = ["Multiple supporting query hits"]
                    }
                ]
            };
        }

    }
}