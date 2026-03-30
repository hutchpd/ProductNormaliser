using ProductNormaliser.Application.AI;
using ProductNormaliser.Application.Categories;
using ProductNormaliser.Application.Governance;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.Discovery)]
public sealed class DiscoveryRunServiceTests
{
    [Test]
    public async Task CreateAsync_QueuesRunAndRecordsAudit()
    {
        var runStore = new FakeDiscoveryRunStore();
        var candidateStore = new FakeDiscoveryRunCandidateStore();
        var categoryService = new FakeCategoryMetadataService(
            new CategoryMetadata { CategoryKey = "tv", DisplayName = "TV", IsEnabled = true },
            new CategoryMetadata { CategoryKey = "monitor", DisplayName = "Monitor", IsEnabled = true });
        var sourceManagement = new RecordingSourceManagementService();
        var audit = new RecordingAuditService();
        var service = new DiscoveryRunService(runStore, candidateStore, categoryService, sourceManagement, audit, new FixedLlmStatusProvider("active", "Loaded."));

        var run = await service.CreateAsync(new CreateDiscoveryRunRequest
        {
            CategoryKeys = [" monitor ", "tv", "tv"],
            Market = " UK ",
            Locale = " en-GB ",
            BrandHints = [" Sony ", "LG", "sony"],
            AutomationMode = SourceAutomationModes.SuggestAccept,
            MaxCandidates = 25
        });

        var stored = await runStore.GetAsync(run.RunId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(stored, Is.Not.Null);
            Assert.That(run.Status, Is.EqualTo(DiscoveryRunStatuses.Queued));
            Assert.That(run.CurrentStage, Is.EqualTo(DiscoveryRunStageNames.Search));
            Assert.That(run.RequestedCategoryKeys, Is.EqualTo(new[] { "monitor", "tv" }));
            Assert.That(run.BrandHints, Is.EqualTo(new[] { "LG", "Sony" }));
            Assert.That(run.Market, Is.EqualTo("UK"));
            Assert.That(run.Locale, Is.EqualTo("en-GB"));
            Assert.That(run.LlmStatus, Is.EqualTo("active"));
            Assert.That(audit.Actions, Does.Contain("discovery_run_created"));
        });
    }

    [Test]
    public async Task PauseResumeAndStop_EnforceRunTransitions()
    {
        var runStore = new FakeDiscoveryRunStore(
            CreateRun("run_queued", DiscoveryRunStatuses.Queued),
            CreateRun("run_running", DiscoveryRunStatuses.Running),
            CreateRun("run_completed", DiscoveryRunStatuses.Completed));
        var service = CreateService(runStore);

        var paused = await service.PauseAsync("run_queued", CancellationToken.None);
        Assert.That(paused, Is.Not.Null);
        Assert.That(paused!.Status, Is.EqualTo(DiscoveryRunStatuses.Paused));

        var resumed = await service.ResumeAsync("run_queued", CancellationToken.None);
        Assert.That(resumed, Is.Not.Null);
        Assert.That(resumed!.Status, Is.EqualTo(DiscoveryRunStatuses.Queued));

        var stopRequested = await service.StopAsync("run_running", CancellationToken.None);
        var invalidResume = async () => await service.ResumeAsync("run_completed", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(stopRequested, Is.Not.Null);
            Assert.That(stopRequested!.Status, Is.EqualTo(DiscoveryRunStatuses.CancelRequested));
            Assert.That(stopRequested.CancelRequestedUtc, Is.Not.Null);
            Assert.That(invalidResume, Throws.TypeOf<InvalidOperationException>());
        });
    }

    [Test]
    public async Task AcceptCandidateAsync_RegistersSuggestedCandidate()
    {
        var runStore = new FakeDiscoveryRunStore(CreateRun("run_1", DiscoveryRunStatuses.Completed));
        var candidateStore = new FakeDiscoveryRunCandidateStore(CreateCandidate("run_1", "safe_shop", DiscoveryRunCandidateStates.Suggested));
        var sourceManagement = new RecordingSourceManagementService();
        var service = CreateService(runStore, candidateStore, sourceManagement);

        var candidate = await service.AcceptCandidateAsync("run_1", "safe_shop", 1, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(candidate, Is.Not.Null);
            Assert.That(candidate!.State, Is.EqualTo(DiscoveryRunCandidateStates.ManuallyAccepted));
            Assert.That(candidate.PreviousState, Is.EqualTo(DiscoveryRunCandidateStates.Suggested));
            Assert.That(candidate.AcceptedSourceId, Is.EqualTo("safe_shop"));
            Assert.That(candidate.Revision, Is.EqualTo(3));
            Assert.That(sourceManagement.Registrations, Has.Count.EqualTo(1));
            Assert.That(sourceManagement.Registrations[0].SupportedCategoryKeys, Is.EqualTo(new[] { "tv" }));
        });
    }

    [Test]
    public async Task DismissAndRestoreCandidate_RoundTripPreviousState()
    {
        var runStore = new FakeDiscoveryRunStore(CreateRun("run_1", DiscoveryRunStatuses.Completed));
        var candidateStore = new FakeDiscoveryRunCandidateStore(CreateCandidate("run_1", "safe_shop", DiscoveryRunCandidateStates.Suggested));
        var dispositionStore = new FakeDiscoveryRunCandidateDispositionStore();
        var service = CreateService(runStore, candidateStore, dispositionStore: dispositionStore);

        var dismissed = await service.DismissCandidateAsync("run_1", "safe_shop", 1, CancellationToken.None);
        Assert.That(dismissed, Is.Not.Null);
        Assert.That(dismissed!.State, Is.EqualTo(DiscoveryRunCandidateStates.Dismissed));
        Assert.That(dismissed.PreviousState, Is.EqualTo(DiscoveryRunCandidateStates.Suggested));

        var activeDisposition = dispositionStore.Items.Single();
        Assert.Multiple(() =>
        {
            Assert.That(activeDisposition.State, Is.EqualTo(DiscoveryRunCandidateStates.Dismissed));
            Assert.That(activeDisposition.IsActive, Is.True);
            Assert.That(activeDisposition.ScopeFingerprint, Is.EqualTo("market:uk|locale:en-gb|categories:tv"));
        });

        var restored = await service.RestoreCandidateAsync("run_1", "safe_shop", 2, CancellationToken.None);
        var restoredDisposition = dispositionStore.Items.Single();

        Assert.Multiple(() =>
        {
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored!.State, Is.EqualTo(DiscoveryRunCandidateStates.Suggested));
            Assert.That(restored.StateMessage, Is.EqualTo("Restored to the active candidate queue."));
            Assert.That(restoredDisposition.IsActive, Is.False);
            Assert.That(restoredDisposition.RestoredUtc, Is.Not.Null);
        });
    }

    [Test]
    public async Task AcceptCandidateAsync_SupersedesMatchingInFlightDuplicatesImmediately()
    {
        var runStore = new FakeDiscoveryRunStore(CreateRun("run_1", DiscoveryRunStatuses.Running));
        var primary = CreateCandidate("run_1", "safe_shop", DiscoveryRunCandidateStates.Suggested);
        var duplicate = CreateCandidate("run_1", "safe_shop_duplicate", DiscoveryRunCandidateStates.Suggested);
        duplicate.BaseUrl = "https://safe.example/";
        duplicate.Host = "safe.example";

        var candidateStore = new FakeDiscoveryRunCandidateStore(primary, duplicate);
        var service = CreateService(runStore, candidateStore, new RecordingSourceManagementService());

        var accepted = await service.AcceptCandidateAsync("run_1", "safe_shop", 1, CancellationToken.None);
        var superseded = await candidateStore.GetAsync("run_1", "safe_shop_duplicate", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(accepted, Is.Not.Null);
            Assert.That(superseded, Is.Not.Null);
            Assert.That(superseded!.State, Is.EqualTo(DiscoveryRunCandidateStates.Superseded));
            Assert.That(superseded.SupersededByCandidateKey, Is.EqualTo("safe_shop"));
            Assert.That(superseded.PreviousState, Is.EqualTo(DiscoveryRunCandidateStates.Suggested));
        });
    }

    [Test]
    public void AcceptCandidateAsync_RejectsStaleRevision()
    {
        var runStore = new FakeDiscoveryRunStore(CreateRun("run_1", DiscoveryRunStatuses.Running));
        var candidateStore = new FakeDiscoveryRunCandidateStore(CreateCandidate("run_1", "safe_shop", DiscoveryRunCandidateStates.Suggested));
        var service = CreateService(runStore, candidateStore, new RecordingSourceManagementService());

        var action = async () => await service.AcceptCandidateAsync("run_1", "safe_shop", 99, CancellationToken.None);

        Assert.That(action, Throws.TypeOf<InvalidOperationException>().With.Message.Contains("changed while this action was in progress"));
    }

    [Test]
    public void AcceptCandidateAsync_RejectsInvalidCandidateTransitions()
    {
        var runStore = new FakeDiscoveryRunStore(CreateRun("run_1", DiscoveryRunStatuses.Completed));
        var candidateStore = new FakeDiscoveryRunCandidateStore(CreateCandidate("run_1", "failed_shop", DiscoveryRunCandidateStates.Failed));
        var service = CreateService(runStore, candidateStore);

        var action = async () => await service.AcceptCandidateAsync("run_1", "failed_shop", 1, CancellationToken.None);

        Assert.That(action, Throws.TypeOf<InvalidOperationException>());
    }

    private static DiscoveryRunService CreateService(
        FakeDiscoveryRunStore runStore,
        FakeDiscoveryRunCandidateStore? candidateStore = null,
        RecordingSourceManagementService? sourceManagement = null,
        FakeDiscoveryRunCandidateDispositionStore? dispositionStore = null)
    {
        return new DiscoveryRunService(
            runStore,
            candidateStore ?? new FakeDiscoveryRunCandidateStore(),
            dispositionStore ?? new FakeDiscoveryRunCandidateDispositionStore(),
            new FakeCategoryMetadataService(new CategoryMetadata { CategoryKey = "tv", DisplayName = "TV", IsEnabled = true }),
            sourceManagement ?? new RecordingSourceManagementService(),
            new RecordingAuditService(),
            new FixedLlmStatusProvider("disabled", "Disabled."));
    }

    private static DiscoveryRun CreateRun(string runId, string status)
    {
        return new DiscoveryRun
        {
            RunId = runId,
            RequestedCategoryKeys = ["tv"],
            Market = "UK",
            Locale = "en-GB",
            AutomationMode = SourceAutomationModes.SuggestAccept,
            Status = status,
            CurrentStage = DiscoveryRunStageNames.Search,
            LlmStatus = "disabled",
            LlmStatusMessage = "Disabled.",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
    }

    private static DiscoveryRunCandidate CreateCandidate(string runId, string candidateKey, string state)
    {
        return new DiscoveryRunCandidate
        {
            Id = $"{runId}:{candidateKey}",
            RunId = runId,
            CandidateKey = candidateKey,
            State = state,
            Revision = 1,
            DisplayName = "Safe Shop",
            BaseUrl = "https://safe.example/",
            Host = "safe.example",
            CandidateType = "retailer",
            AllowedMarkets = ["UK"],
            PreferredLocale = "en-GB",
            MatchedCategoryKeys = ["tv"],
            AllowedByGovernance = true,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
    }

    private sealed class FakeDiscoveryRunStore(params DiscoveryRun[] runs) : IDiscoveryRunStore
    {
        private readonly Dictionary<string, DiscoveryRun> items = runs.ToDictionary(run => run.RunId, StringComparer.OrdinalIgnoreCase);

        public Task<DiscoveryRunPage> ListAsync(DiscoveryRunQuery query, CancellationToken cancellationToken = default)
        {
            var results = items.Values
                .Where(run => string.IsNullOrWhiteSpace(query.Status) || string.Equals(run.Status, query.Status, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(run => run.UpdatedUtc)
                .ToArray();
            return Task.FromResult(new DiscoveryRunPage
            {
                Items = results,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = results.Length
            });
        }

        public Task<DiscoveryRun?> GetAsync(string runId, CancellationToken cancellationToken = default)
            => Task.FromResult(items.TryGetValue(runId, out var run) ? run : null);

        public Task<DiscoveryRun?> GetNextQueuedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(items.Values.OrderBy(run => run.CreatedUtc).FirstOrDefault(run => string.Equals(run.Status, DiscoveryRunStatuses.Queued, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyList<DiscoveryRun>> ListByStatusesAsync(IReadOnlyCollection<string> statuses, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DiscoveryRun>>(items.Values.Where(run => statuses.Contains(run.Status)).ToArray());

        public Task UpsertAsync(DiscoveryRun run, CancellationToken cancellationToken = default)
        {
            items[run.RunId] = run;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDiscoveryRunCandidateStore(params DiscoveryRunCandidate[] candidates) : IDiscoveryRunCandidateStore
    {
        private readonly Dictionary<string, DiscoveryRunCandidate> items = candidates.ToDictionary(candidate => $"{candidate.RunId}:{candidate.CandidateKey}", StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<DiscoveryRunCandidate>> ListByRunAsync(string runId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DiscoveryRunCandidate>>(items.Values.Where(candidate => string.Equals(candidate.RunId, runId, StringComparison.OrdinalIgnoreCase)).ToArray());

        public Task<DiscoveryRunCandidatePage> QueryByRunAsync(string runId, DiscoveryRunCandidateQuery query, CancellationToken cancellationToken = default)
        {
            var results = items.Values.Where(candidate => string.Equals(candidate.RunId, runId, StringComparison.OrdinalIgnoreCase)).ToArray();
            return Task.FromResult(new DiscoveryRunCandidatePage
            {
                Items = results,
                StateFilter = query.StateFilter ?? DiscoveryRunCandidateStateFilters.All,
                Sort = query.Sort ?? DiscoveryRunCandidateSortModes.ReviewPriority,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = results.Length,
                Summary = new DiscoveryRunCandidateRunSummary
                {
                    RunCandidateCount = results.Length,
                    ActiveCandidateCount = results.Length,
                    ArchivedCandidateCount = 0
                }
            });
        }

        public Task<DiscoveryRunCandidate?> GetAsync(string runId, string candidateKey, CancellationToken cancellationToken = default)
            => Task.FromResult(items.TryGetValue($"{runId}:{candidateKey}", out var candidate) ? candidate : null);

        public Task UpsertAsync(DiscoveryRunCandidate candidate, CancellationToken cancellationToken = default)
        {
            items[$"{candidate.RunId}:{candidate.CandidateKey}"] = candidate;
            return Task.CompletedTask;
        }

        public Task<bool> TryUpdateAsync(DiscoveryRunCandidate candidate, int expectedRevision, CancellationToken cancellationToken = default)
        {
            var key = $"{candidate.RunId}:{candidate.CandidateKey}";
            if (!items.TryGetValue(key, out var existing) || existing.Revision != expectedRevision)
            {
                return Task.FromResult(false);
            }

            items[key] = candidate;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeDiscoveryRunCandidateDispositionStore : IDiscoveryRunCandidateDispositionStore
    {
        private readonly Dictionary<string, DiscoveryRunCandidateDisposition> items = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<DiscoveryRunCandidateDisposition> Items => items.Values.ToArray();

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

    private sealed class FakeCategoryMetadataService(params CategoryMetadata[] categories) : ICategoryMetadataService
    {
        private readonly IReadOnlyList<CategoryMetadata> items = categories;

        public Task EnsureDefaultsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<CategoryMetadata>> ListAsync(bool enabledOnly = false, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CategoryMetadata>>(enabledOnly ? items.Where(category => category.IsEnabled).ToArray() : items);
        public Task<CategoryMetadata?> GetAsync(string categoryKey, CancellationToken cancellationToken = default)
            => Task.FromResult(items.FirstOrDefault(category => string.Equals(category.CategoryKey, categoryKey, StringComparison.OrdinalIgnoreCase)));
        public Task<CategoryMetadata> UpsertAsync(CategoryMetadata categoryMetadata, CancellationToken cancellationToken = default) => Task.FromResult(categoryMetadata);
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

    private sealed class RecordingAuditService : IManagementAuditService
    {
        public List<string> Actions { get; } = [];

        public Task RecordAsync(string action, string targetType, string targetId, IReadOnlyDictionary<string, string>? details = null, CancellationToken cancellationToken = default)
        {
            Actions.Add(action);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ManagementAuditEntry>> ListRecentAsync(int take = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ManagementAuditEntry>>([]);
    }

    private sealed class FixedLlmStatusProvider(string code, string message) : ILlmStatusProvider
    {
        public LlmServiceStatus GetStatus() => new() { Code = code, Message = message };
    }
}
