using Microsoft.Extensions.Options;
using ProductNormaliser.Application.Governance;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Tests;

public sealed class DiscoveryRunProcessorTests
{
    [Test]
    public async Task ProcessNextAsync_CompletesRunAndPublishesAutoAcceptedCandidate()
    {
        var run = CreateQueuedRun(SourceAutomationModes.AutoAcceptAndSeed, llmStatus: "active");
        var runStore = new FakeDiscoveryRunStore(run);
        var candidateStore = new FakeDiscoveryRunCandidateStore();
        var crawlSourceStore = new FakeCrawlSourceStore();
        var sourceManagement = new RecordingSourceManagementService();
        var searchProvider = new FixedSearchProvider(CreateSearchCandidate());
        var probeService = new FixedProbeService(new SourceCandidateProbeResult
        {
            HomePageReachable = true,
            RobotsTxtReachable = true,
            SitemapDetected = true,
            CrawlabilityScore = 95m,
            CategoryRelevanceScore = 92m,
            ExtractabilityScore = 96m,
            CatalogLikelihoodScore = 94m,
            RepresentativeCategoryPageUrl = "https://safe.example/tv",
            RepresentativeCategoryPageReachable = true,
            RepresentativeProductPageUrl = "https://safe.example/tv-1",
            RepresentativeProductPageReachable = true,
            RuntimeExtractionCompatible = true,
            RepresentativeRuntimeProductCount = 4,
            AutomationReachableCategorySampleCount = 3,
            AutomationReachableProductSampleCount = 3,
            AutomationRuntimeCompatibleProductSampleCount = 3,
            AutomationStructuredProductEvidenceSampleCount = 3,
            StructuredProductEvidenceDetected = true,
            TechnicalAttributeEvidenceDetected = true,
            LlmAcceptedRepresentativeProductPage = true,
            LlmConfidenceScore = 96m,
            ProbeElapsedMs = 120,
            LlmElapsedMs = 250
        });
        var processor = CreateProcessor(runStore, candidateStore, crawlSourceStore, sourceManagement, searchProvider, probeService);

        var processed = await processor.ProcessNextAsync(CancellationToken.None);
        var storedRun = await runStore.GetAsync(run.RunId, CancellationToken.None);
        var candidates = await candidateStore.ListByRunAsync(run.RunId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.True);
            Assert.That(storedRun, Is.Not.Null);
            Assert.That(storedRun!.Status, Is.EqualTo(DiscoveryRunStatuses.Completed));
            Assert.That(storedRun.CurrentStage, Is.EqualTo(DiscoveryRunStageNames.Publish));
            Assert.That(storedRun.SearchResultCount, Is.EqualTo(1));
            Assert.That(storedRun.CollapsedCandidateCount, Is.EqualTo(1));
            Assert.That(storedRun.ProbeCompletedCount, Is.EqualTo(1));
            Assert.That(storedRun.LlmCompletedCount, Is.EqualTo(1));
            Assert.That(storedRun.LlmAverageElapsedMs, Is.EqualTo(250));
            Assert.That(storedRun.PublishedCandidateCount, Is.EqualTo(1));
            Assert.That(candidates, Has.Count.EqualTo(1));
            Assert.That(candidates[0].State, Is.EqualTo(DiscoveryRunCandidateStates.AutoAccepted));
            Assert.That(candidates[0].AcceptedSourceId, Is.EqualTo("safe_shop"));
            Assert.That(sourceManagement.Registrations.Select(item => item.SourceId), Is.EqualTo(new[] { "safe_shop" }));
        });
    }

    [Test]
    public async Task ProcessNextAsync_SupersedesDuplicateCandidatesAfterImmediateAutoAccept()
    {
        var run = CreateQueuedRun(SourceAutomationModes.AutoAcceptAndSeed, llmStatus: "active");
        var runStore = new FakeDiscoveryRunStore(run);
        var candidateStore = new FakeDiscoveryRunCandidateStore();
        var crawlSourceStore = new FakeCrawlSourceStore();
        var sourceManagement = new RecordingSourceManagementService();
        var searchProvider = new FixedSearchProvider(CreateSearchCandidate(), CreateDuplicateSearchCandidate());
        var probeService = new FixedProbeService(CreateStrongProbeResult());
        var processor = CreateProcessor(runStore, candidateStore, crawlSourceStore, sourceManagement, searchProvider, probeService);

        await processor.ProcessNextAsync(CancellationToken.None);
        var candidates = await candidateStore.ListByRunAsync(run.RunId, CancellationToken.None);
        var duplicate = candidates.Single(candidate => string.Equals(candidate.CandidateKey, "safe_shop_duplicate", StringComparison.OrdinalIgnoreCase));

        Assert.Multiple(() =>
        {
            Assert.That(candidates.Count, Is.EqualTo(2));
            Assert.That(duplicate.State, Is.EqualTo(DiscoveryRunCandidateStates.Superseded));
            Assert.That(duplicate.SupersededByCandidateKey, Is.EqualTo("safe_shop"));
        });
    }

    [Test]
    public async Task ProcessNextAsync_DoesNotOverwriteCandidateAcceptedByOperatorDuringRun()
    {
        var run = CreateQueuedRun(SourceAutomationModes.AutoAcceptAndSeed, llmStatus: "active");
        var runStore = new FakeDiscoveryRunStore(run);
        var candidateStore = new FakeDiscoveryRunCandidateStore();
        candidateStore.BeforeTryUpdate = candidate =>
        {
            if (string.Equals(candidate.CandidateKey, "safe_shop", StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.State, DiscoveryRunCandidateStates.AutoAccepted, StringComparison.OrdinalIgnoreCase))
            {
                candidateStore.UpsertAsync(new DiscoveryRunCandidate
                {
                    Id = candidate.Id,
                    RunId = candidate.RunId,
                    CandidateKey = candidate.CandidateKey,
                    Revision = candidate.Revision + 5,
                    State = DiscoveryRunCandidateStates.ManuallyAccepted,
                    PreviousState = DiscoveryRunCandidateStates.Suggested,
                    AcceptedSourceId = candidate.CandidateKey,
                    StateMessage = "Accepted by operator.",
                    DisplayName = candidate.DisplayName,
                    BaseUrl = candidate.BaseUrl,
                    Host = candidate.Host,
                    CandidateType = candidate.CandidateType,
                    AllowedMarkets = candidate.AllowedMarkets,
                    PreferredLocale = candidate.PreferredLocale,
                    MarketEvidence = candidate.MarketEvidence,
                    LocaleEvidence = candidate.LocaleEvidence,
                    ConfidenceScore = candidate.ConfidenceScore,
                    CrawlabilityScore = candidate.CrawlabilityScore,
                    ExtractabilityScore = candidate.ExtractabilityScore,
                    DuplicateRiskScore = candidate.DuplicateRiskScore,
                    RecommendationStatus = candidate.RecommendationStatus,
                    RuntimeExtractionStatus = candidate.RuntimeExtractionStatus,
                    RuntimeExtractionMessage = candidate.RuntimeExtractionMessage,
                    MatchedCategoryKeys = candidate.MatchedCategoryKeys,
                    MatchedBrandHints = candidate.MatchedBrandHints,
                    AlreadyRegistered = candidate.AlreadyRegistered,
                    DuplicateSourceIds = candidate.DuplicateSourceIds,
                    DuplicateSourceDisplayNames = candidate.DuplicateSourceDisplayNames,
                    AllowedByGovernance = candidate.AllowedByGovernance,
                    GovernanceWarning = candidate.GovernanceWarning,
                    Probe = candidate.Probe,
                    AutomationAssessment = candidate.AutomationAssessment,
                    Reasons = candidate.Reasons,
                    CreatedUtc = candidate.CreatedUtc,
                    UpdatedUtc = DateTime.UtcNow,
                    DecisionUtc = DateTime.UtcNow
                }, CancellationToken.None).GetAwaiter().GetResult();
            }
        };

        var processor = CreateProcessor(
            runStore,
            candidateStore,
            new FakeCrawlSourceStore(),
            new RecordingSourceManagementService(),
            new FixedSearchProvider(CreateSearchCandidate()),
            new FixedProbeService(CreateStrongProbeResult()));

        await processor.ProcessNextAsync(CancellationToken.None);
        var candidate = await candidateStore.GetAsync(run.RunId, "safe_shop", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(candidate, Is.Not.Null);
            Assert.That(candidate!.State, Is.EqualTo(DiscoveryRunCandidateStates.ManuallyAccepted));
            Assert.That(candidate.AcceptedSourceId, Is.EqualTo("safe_shop"));
        });
    }

    [Test]
    public async Task ProcessNextAsync_ArchivesCandidateSuppressedByHistoricalDismissal()
    {
        var run = CreateQueuedRun(SourceAutomationModes.OperatorAssisted, llmStatus: "disabled");
        var runStore = new FakeDiscoveryRunStore(run);
        var candidateStore = new FakeDiscoveryRunCandidateStore();
        var dispositionStore = new FakeDiscoveryRunCandidateDispositionStore(
            new DiscoveryRunCandidateDisposition
            {
                Id = "disp_1",
                State = DiscoveryRunCandidateStates.Dismissed,
                ScopeFingerprint = "market:uk|locale:en-gb|categories:tv",
                RequestedCategoryKeys = ["tv"],
                Market = run.Market,
                Locale = run.Locale,
                NormalizedHost = DiscoveryRunCandidateIdentity.NormalizeHost("safe.example"),
                NormalizedBaseUrl = DiscoveryRunCandidateIdentity.NormalizeBaseUrl("https://safe.example/"),
                NormalizedDisplayName = DiscoveryRunCandidateIdentity.NormalizeName("Safe Shop"),
                AllowedMarkets = ["UK"],
                SourceRunId = "prior_run",
                SourceCandidateKey = "safe_shop",
                IsActive = true,
                CreatedUtc = DateTime.UtcNow.AddDays(-2),
                UpdatedUtc = DateTime.UtcNow.AddDays(-1)
            });
        var probeService = new FixedProbeService(CreateStrongProbeResult());
        var processor = CreateProcessor(
            runStore,
            candidateStore,
            dispositionStore,
            new FakeCrawlSourceStore(),
            new RecordingSourceManagementService(),
            new FixedSearchProvider(CreateSearchCandidate()),
            probeService);

        await processor.ProcessNextAsync(CancellationToken.None);
        var candidate = await candidateStore.GetAsync(run.RunId, "safe_shop", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(candidate, Is.Not.Null);
            Assert.That(candidate!.State, Is.EqualTo(DiscoveryRunCandidateStates.Archived));
            Assert.That(candidate.ArchiveReason, Is.EqualTo("historical_dismissal"));
            Assert.That(candidate.StateMessage, Does.Contain("previously dismissed"));
            Assert.That(candidate.SuppressionDispositionId, Is.EqualTo("disp_1"));
            Assert.That(probeService.Invocations, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task ProcessNextAsync_StopsWhenOperatorRequestsCancellation()
    {
        var run = CreateQueuedRun(SourceAutomationModes.SuggestAccept, llmStatus: "disabled");
        var runStore = new FakeDiscoveryRunStore(run)
        {
            OnGet = current =>
            {
                if (string.Equals(current.Status, DiscoveryRunStatuses.Running, StringComparison.OrdinalIgnoreCase))
                {
                    current.Status = DiscoveryRunStatuses.CancelRequested;
                    current.CancelRequestedUtc = DateTime.UtcNow;
                }
            }
        };
        var candidateStore = new FakeDiscoveryRunCandidateStore();
        var processor = CreateProcessor(runStore, candidateStore, new FakeCrawlSourceStore(), new RecordingSourceManagementService(), new FixedSearchProvider(CreateSearchCandidate()), new FixedProbeService(new SourceCandidateProbeResult()));

        var processed = await processor.ProcessNextAsync(CancellationToken.None);
        var storedRun = await runStore.GetAsync(run.RunId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.True);
            Assert.That(storedRun, Is.Not.Null);
            Assert.That(storedRun!.Status, Is.EqualTo(DiscoveryRunStatuses.Cancelled));
            Assert.That(storedRun.CompletedUtc, Is.Not.Null);
            Assert.That(storedRun.StatusMessage, Does.Contain("cancelled"));
            Assert.That(candidateStore.StoredCandidates, Is.Empty);
        });
    }

    [Test]
    public async Task ProcessNextAsync_LeavesRunPausedWhenOperatorPausesDuringExecution()
    {
        var run = CreateQueuedRun(SourceAutomationModes.SuggestAccept, llmStatus: "disabled");
        var runStore = new FakeDiscoveryRunStore(run)
        {
            OnGet = current =>
            {
                if (string.Equals(current.Status, DiscoveryRunStatuses.Running, StringComparison.OrdinalIgnoreCase))
                {
                    current.Status = DiscoveryRunStatuses.Paused;
                }
            }
        };
        var candidateStore = new FakeDiscoveryRunCandidateStore();
        var processor = CreateProcessor(runStore, candidateStore, new FakeCrawlSourceStore(), new RecordingSourceManagementService(), new FixedSearchProvider(CreateSearchCandidate()), new FixedProbeService(new SourceCandidateProbeResult()));

        var processed = await processor.ProcessNextAsync(CancellationToken.None);
        var storedRun = await runStore.GetAsync(run.RunId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.True);
            Assert.That(storedRun, Is.Not.Null);
            Assert.That(storedRun!.Status, Is.EqualTo(DiscoveryRunStatuses.Paused));
            Assert.That(storedRun.CompletedUtc, Is.Null);
            Assert.That(storedRun.StatusMessage, Does.Contain("paused"));
            Assert.That(candidateStore.StoredCandidates, Is.Empty);
        });
    }

    private static DiscoveryRunProcessor CreateProcessor(
        FakeDiscoveryRunStore runStore,
        FakeDiscoveryRunCandidateStore candidateStore,
        FakeDiscoveryRunCandidateDispositionStore dispositionStore,
        FakeCrawlSourceStore crawlSourceStore,
        RecordingSourceManagementService sourceManagement,
        FixedSearchProvider searchProvider,
        FixedProbeService probeService)
    {
        return new DiscoveryRunProcessor(
            runStore,
            candidateStore,
            dispositionStore,
            crawlSourceStore,
            sourceManagement,
            new PermissiveGovernanceService(),
            searchProvider,
            probeService,
            Options.Create(new SourceOnboardingAutomationOptions()),
            Options.Create(new DiscoveryRunOperationsOptions()));
    }

    private static DiscoveryRunProcessor CreateProcessor(
        FakeDiscoveryRunStore runStore,
        FakeDiscoveryRunCandidateStore candidateStore,
        FakeCrawlSourceStore crawlSourceStore,
        RecordingSourceManagementService sourceManagement,
        FixedSearchProvider searchProvider,
        FixedProbeService probeService)
    {
        return CreateProcessor(
            runStore,
            candidateStore,
            new FakeDiscoveryRunCandidateDispositionStore(),
            crawlSourceStore,
            sourceManagement,
            searchProvider,
            probeService);
    }

    private static DiscoveryRun CreateQueuedRun(string automationMode, string llmStatus)
    {
        return new DiscoveryRun
        {
            RunId = "discovery_run_1",
            RequestedCategoryKeys = ["tv"],
            Market = "UK",
            Locale = "en-GB",
            AutomationMode = automationMode,
            BrandHints = ["Sony"],
            MaxCandidates = 10,
            Status = DiscoveryRunStatuses.Queued,
            CurrentStage = DiscoveryRunStageNames.Search,
            LlmStatus = llmStatus,
            LlmStatusMessage = llmStatus,
            CreatedUtc = DateTime.UtcNow.AddMinutes(-1),
            UpdatedUtc = DateTime.UtcNow.AddMinutes(-1)
        };
    }

    private static SourceCandidateSearchResult CreateSearchCandidate()
    {
        return new SourceCandidateSearchResult
        {
            CandidateKey = "safe_shop",
            DisplayName = "Safe Shop",
            BaseUrl = "https://safe.example/",
            Host = "safe.example",
            CandidateType = "retailer",
            AllowedMarkets = ["UK"],
            PreferredLocale = "en-GB",
            MarketEvidence = "explicit",
            LocaleEvidence = "explicit",
            MatchedCategoryKeys = ["tv"],
            MatchedBrandHints = ["Sony"],
            SearchReasons = ["Strong product signals."]
        };
    }

    private static SourceCandidateSearchResult CreateDuplicateSearchCandidate()
    {
        return new SourceCandidateSearchResult
        {
            CandidateKey = "safe_shop_duplicate",
            DisplayName = "Safe Shop",
            BaseUrl = "https://safe-shop.co.uk/",
            Host = "safe-shop.co.uk",
            CandidateType = "retailer",
            AllowedMarkets = ["UK"],
            PreferredLocale = "en-GB",
            MarketEvidence = "explicit",
            LocaleEvidence = "explicit",
            MatchedCategoryKeys = ["tv"],
            MatchedBrandHints = ["Sony"],
            SearchReasons = ["Mirror host."]
        };
    }

    private static SourceCandidateProbeResult CreateStrongProbeResult()
    {
        return new SourceCandidateProbeResult
        {
            HomePageReachable = true,
            RobotsTxtReachable = true,
            SitemapDetected = true,
            CrawlabilityScore = 95m,
            CategoryRelevanceScore = 92m,
            ExtractabilityScore = 96m,
            CatalogLikelihoodScore = 94m,
            RepresentativeCategoryPageUrl = "https://safe.example/tv",
            RepresentativeCategoryPageReachable = true,
            RepresentativeProductPageUrl = "https://safe.example/tv-1",
            RepresentativeProductPageReachable = true,
            RuntimeExtractionCompatible = true,
            RepresentativeRuntimeProductCount = 4,
            AutomationReachableCategorySampleCount = 3,
            AutomationReachableProductSampleCount = 3,
            AutomationRuntimeCompatibleProductSampleCount = 3,
            AutomationStructuredProductEvidenceSampleCount = 3,
            StructuredProductEvidenceDetected = true,
            TechnicalAttributeEvidenceDetected = true,
            LlmAcceptedRepresentativeProductPage = true,
            LlmConfidenceScore = 96m,
            ProbeElapsedMs = 120,
            LlmElapsedMs = 250
        };
    }

    private sealed class FakeDiscoveryRunStore(DiscoveryRun run) : IDiscoveryRunStore
    {
        public Action<DiscoveryRun>? OnGet { get; set; }
        private readonly Dictionary<string, DiscoveryRun> items = new(StringComparer.OrdinalIgnoreCase)
        {
            [run.RunId] = run
        };

        public Task<DiscoveryRunPage> ListAsync(DiscoveryRunQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(new DiscoveryRunPage { Items = items.Values.ToArray(), Page = query.Page, PageSize = query.PageSize, TotalCount = items.Count });

        public Task<DiscoveryRun?> GetAsync(string runId, CancellationToken cancellationToken = default)
        {
            if (!items.TryGetValue(runId, out var run))
            {
                return Task.FromResult<DiscoveryRun?>(null);
            }

            OnGet?.Invoke(run);
            return Task.FromResult<DiscoveryRun?>(run);
        }

        public Task<DiscoveryRun?> GetNextQueuedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(items.Values.FirstOrDefault(item => string.Equals(item.Status, DiscoveryRunStatuses.Queued, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyList<DiscoveryRun>> ListByStatusesAsync(IReadOnlyCollection<string> statuses, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DiscoveryRun>>(items.Values.Where(run => statuses.Contains(run.Status)).ToArray());

        public Task UpsertAsync(DiscoveryRun run, CancellationToken cancellationToken = default)
        {
            items[run.RunId] = run;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDiscoveryRunCandidateStore : IDiscoveryRunCandidateStore
    {
        private readonly Dictionary<string, DiscoveryRunCandidate> items = new(StringComparer.OrdinalIgnoreCase);
        public Action<DiscoveryRunCandidate>? BeforeTryUpdate { get; set; }
        public IReadOnlyList<DiscoveryRunCandidate> StoredCandidates => items.Values.ToArray();

        public Task<IReadOnlyList<DiscoveryRunCandidate>> ListByRunAsync(string runId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DiscoveryRunCandidate>>(items.Values.Where(candidate => string.Equals(candidate.RunId, runId, StringComparison.OrdinalIgnoreCase)).OrderByDescending(candidate => candidate.ConfidenceScore).ToArray());

        public Task<DiscoveryRunCandidate?> GetAsync(string runId, string candidateKey, CancellationToken cancellationToken = default)
            => Task.FromResult(items.TryGetValue($"{runId}:{candidateKey}", out var candidate) ? candidate : null);

        public Task UpsertAsync(DiscoveryRunCandidate candidate, CancellationToken cancellationToken = default)
        {
            items[$"{candidate.RunId}:{candidate.CandidateKey}"] = candidate;
            return Task.CompletedTask;
        }

        public Task<bool> TryUpdateAsync(DiscoveryRunCandidate candidate, int expectedRevision, CancellationToken cancellationToken = default)
        {
            BeforeTryUpdate?.Invoke(candidate);

            var key = $"{candidate.RunId}:{candidate.CandidateKey}";
            if (!items.TryGetValue(key, out var existing) || existing.Revision != expectedRevision)
            {
                return Task.FromResult(false);
            }

            items[key] = candidate;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeDiscoveryRunCandidateDispositionStore(params DiscoveryRunCandidateDisposition[] dispositions) : IDiscoveryRunCandidateDispositionStore
    {
        private readonly Dictionary<string, DiscoveryRunCandidateDisposition> items = dispositions.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<DiscoveryRunCandidateDisposition>> FindActiveMatchesAsync(
            string scopeFingerprint,
            string normalizedHost,
            string normalizedBaseUrl,
            string normalizedDisplayName,
            IReadOnlyCollection<string> allowedMarkets,
            CancellationToken cancellationToken = default)
        {
            var matches = items.Values
                .Where(item => item.IsActive
                    && string.Equals(item.ScopeFingerprint, scopeFingerprint, StringComparison.OrdinalIgnoreCase)
                    && (string.Equals(item.NormalizedHost, normalizedHost, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(item.NormalizedBaseUrl, normalizedBaseUrl, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(item.NormalizedDisplayName, normalizedDisplayName, StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            return Task.FromResult<IReadOnlyList<DiscoveryRunCandidateDisposition>>(matches);
        }

        public Task UpsertAsync(DiscoveryRunCandidateDisposition disposition, CancellationToken cancellationToken = default)
        {
            items[disposition.Id] = disposition;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCrawlSourceStore : ICrawlSourceStore
    {
        public Task<IReadOnlyList<CrawlSource>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CrawlSource>>([]);
        public Task<CrawlSource?> GetAsync(string sourceId, CancellationToken cancellationToken = default) => Task.FromResult<CrawlSource?>(null);
        public Task UpsertAsync(CrawlSource source, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingSourceManagementService : ISourceManagementService
    {
        public List<CrawlSourceRegistration> Registrations { get; } = [];

        public Task<IReadOnlyList<CrawlSource>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CrawlSource>>([]);
        public Task<CrawlSource?> GetAsync(string sourceId, CancellationToken cancellationToken = default) => Task.FromResult<CrawlSource?>(null);
        public Task<CrawlSource> RegisterAsync(CrawlSourceRegistration registration, CancellationToken cancellationToken = default)
        {
            Registrations.Add(registration);
            return Task.FromResult(new CrawlSource
            {
                Id = registration.SourceId,
                DisplayName = registration.DisplayName,
                BaseUrl = registration.BaseUrl,
                Host = new Uri(registration.BaseUrl).Host,
                IsEnabled = registration.IsEnabled,
                AllowedMarkets = registration.AllowedMarkets.ToList(),
                PreferredLocale = registration.PreferredLocale ?? "en-GB",
                SupportedCategoryKeys = registration.SupportedCategoryKeys.ToList(),
                AutomationPolicy = registration.AutomationPolicy ?? new SourceAutomationPolicy(),
                ThrottlingPolicy = new SourceThrottlingPolicy(),
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            });
        }
        public Task<CrawlSource> UpdateAsync(string sourceId, CrawlSourceUpdate update, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CrawlSource> EnableAsync(string sourceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CrawlSource> DisableAsync(string sourceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CrawlSource> AssignCategoriesAsync(string sourceId, IReadOnlyCollection<string> categoryKeys, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CrawlSource> SetThrottlingAsync(string sourceId, SourceThrottlingPolicy policy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class PermissiveGovernanceService : ICrawlGovernanceService
    {
        public void ValidateSourceBaseUrl(string baseUrl, string parameterName)
        {
        }

        public void ValidateCrawlRequest(string requestType, IReadOnlyCollection<string> categories, IReadOnlyCollection<string> sources, IReadOnlyCollection<string> productIds, IReadOnlyCollection<ProductNormaliser.Application.Crawls.CrawlJobTargetDescriptor> targets, string parameterName)
        {
        }
    }

    private sealed class FixedSearchProvider(params SourceCandidateSearchResult[] candidates) : ISourceCandidateSearchProvider
    {
        public Task<SourceCandidateSearchResponse> SearchAsync(DiscoverSourceCandidatesRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new SourceCandidateSearchResponse { Candidates = candidates });
    }

    private sealed class FixedProbeService(SourceCandidateProbeResult result) : ISourceCandidateProbeService
    {
        public int Invocations { get; private set; }

        public Task<SourceCandidateProbeResult> ProbeAsync(SourceCandidateSearchResult candidate, IReadOnlyCollection<string> categoryKeys, string automationMode, CancellationToken cancellationToken = default)
        {
            Invocations += 1;
            return Task.FromResult(result);
        }
    }
}
