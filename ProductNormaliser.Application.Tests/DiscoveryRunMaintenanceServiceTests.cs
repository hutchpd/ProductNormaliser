using Microsoft.Extensions.Options;
using ProductNormaliser.Application.Observability;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.Discovery)]
public sealed class DiscoveryRunMaintenanceServiceTests
{
    [Test]
    public async Task SweepAsync_EmitsRecoveryAndArchiveMetrics()
    {
        using var collector = new TelemetryMetricCollector(ProductNormaliserTelemetry.TelemetryName);

        var staleRun = CreateRun("run_recover_metric", DiscoveryRunStatuses.Running);
        staleRun.LastHeartbeatUtc = DateTime.UtcNow.AddMinutes(-30);
        staleRun.UpdatedUtc = staleRun.LastHeartbeatUtc.Value;

        var archivedRun = CreateRun("run_archive_metric", DiscoveryRunStatuses.Completed);
        archivedRun.CompletedUtc = DateTime.UtcNow.AddDays(-2);

        var archivedCandidate = new DiscoveryRunCandidate
        {
            Id = "run_archive_metric:safe_shop",
            RunId = archivedRun.RunId,
            CandidateKey = "safe_shop",
            Revision = 2,
            State = DiscoveryRunCandidateStates.ManuallyAccepted,
            PreviousState = DiscoveryRunCandidateStates.Suggested,
            AcceptedSourceId = "safe_shop",
            DisplayName = "Safe Shop",
            BaseUrl = "https://safe.example/",
            Host = "safe.example",
            CandidateType = "retailer",
            AllowedMarkets = ["UK"],
            PreferredLocale = "en-GB",
            MatchedCategoryKeys = ["tv"],
            AllowedByGovernance = true,
            CreatedUtc = DateTime.UtcNow.AddDays(-4),
            UpdatedUtc = DateTime.UtcNow.AddDays(-2),
            DecisionUtc = DateTime.UtcNow.AddDays(-2)
        };

        var runStore = new FakeDiscoveryRunStore(staleRun, archivedRun);
        var candidateStore = new FakeDiscoveryRunCandidateStore(archivedCandidate);
        var service = CreateService(runStore, candidateStore);

        await service.SweepAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(collector.GetMeasurements("productnormaliser.discovery.runs.recovered").Count, Is.EqualTo(1));
            Assert.That(collector.GetMeasurements("productnormaliser.discovery.candidates.archived").Count, Is.EqualTo(1));
            Assert.That(collector.GetMeasurements("productnormaliser.discovery.candidates.archived").Select(item => item.Tags.Single(tag => tag.Key == "reason").Value), Contains.Item("retention_window_elapsed"));
        });
    }

    [Test]
    public async Task SweepAsync_EmitsFailedRecoveryMetricWhenRecoveryBudgetIsExhausted()
    {
        using var collector = new TelemetryMetricCollector(ProductNormaliserTelemetry.TelemetryName);

        var run = CreateRun("run_failed_metric", DiscoveryRunStatuses.Running);
        run.LastHeartbeatUtc = DateTime.UtcNow.AddMinutes(-30);
        run.UpdatedUtc = run.LastHeartbeatUtc.Value;
        run.RecoveryAttemptCount = 1;

        var service = CreateService(new FakeDiscoveryRunStore(run), new FakeDiscoveryRunCandidateStore(), options: new DiscoveryRunOperationsOptions
        {
            AbandonedHeartbeatTimeoutMinutes = 5,
            MaxRecoveryAttempts = 1,
            CandidateArchiveRetentionHours = 24
        });

        await service.SweepAsync(CancellationToken.None);

        Assert.That(collector.GetMeasurements("productnormaliser.discovery.runs.recovery_failed").Count, Is.EqualTo(1));
    }

    [Test]
    public async Task SweepAsync_RequeuesAbandonedRunningRunWithinRecoveryBudget()
    {
        var run = CreateRun("run_recover", DiscoveryRunStatuses.Running);
        run.LastHeartbeatUtc = DateTime.UtcNow.AddMinutes(-30);
        run.UpdatedUtc = run.LastHeartbeatUtc.Value;

        var runStore = new FakeDiscoveryRunStore(run);
        var candidateStore = new FakeDiscoveryRunCandidateStore();
        var service = CreateService(runStore, candidateStore, options: new DiscoveryRunOperationsOptions
        {
            AbandonedHeartbeatTimeoutMinutes = 5,
            MaxRecoveryAttempts = 2,
            CandidateArchiveRetentionHours = 24
        });

        await service.SweepAsync(CancellationToken.None);
        var updated = await runStore.GetAsync(run.RunId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(updated, Is.Not.Null);
            Assert.That(updated!.Status, Is.EqualTo(DiscoveryRunStatuses.Queued));
            Assert.That(updated.RecoveryAttemptCount, Is.EqualTo(1));
            Assert.That(updated.StatusMessage, Does.Contain("re-queued"));
        });
    }

    [Test]
    public async Task SweepAsync_FailsAbandonedRunningRunAfterRecoveryBudgetIsExhausted()
    {
        var run = CreateRun("run_failed_recovery", DiscoveryRunStatuses.Running);
        run.LastHeartbeatUtc = DateTime.UtcNow.AddMinutes(-30);
        run.UpdatedUtc = run.LastHeartbeatUtc.Value;
        run.RecoveryAttemptCount = 1;

        var runStore = new FakeDiscoveryRunStore(run);
        var candidateStore = new FakeDiscoveryRunCandidateStore();
        var service = CreateService(runStore, candidateStore, options: new DiscoveryRunOperationsOptions
        {
            AbandonedHeartbeatTimeoutMinutes = 5,
            MaxRecoveryAttempts = 1,
            CandidateArchiveRetentionHours = 24
        });

        await service.SweepAsync(CancellationToken.None);
        var updated = await runStore.GetAsync(run.RunId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(updated, Is.Not.Null);
            Assert.That(updated!.Status, Is.EqualTo(DiscoveryRunStatuses.Failed));
            Assert.That(updated.FailureMessage, Does.Contain("exhausted deterministic recovery attempts"));
            Assert.That(updated.CompletedUtc, Is.Not.Null);
        });
    }

    [Test]
    public async Task SweepAsync_ArchivesAcceptedCandidatesAfterRetentionWindow()
    {
        var run = CreateRun("run_archive", DiscoveryRunStatuses.Completed);
        run.CompletedUtc = DateTime.UtcNow.AddDays(-2);

        var candidate = new DiscoveryRunCandidate
        {
            Id = "run_archive:safe_shop",
            RunId = run.RunId,
            CandidateKey = "safe_shop",
            Revision = 2,
            State = DiscoveryRunCandidateStates.ManuallyAccepted,
            PreviousState = DiscoveryRunCandidateStates.Suggested,
            AcceptedSourceId = "safe_shop",
            DisplayName = "Safe Shop",
            BaseUrl = "https://safe.example/",
            Host = "safe.example",
            CandidateType = "retailer",
            AllowedMarkets = ["UK"],
            PreferredLocale = "en-GB",
            MatchedCategoryKeys = ["tv"],
            AllowedByGovernance = true,
            CreatedUtc = DateTime.UtcNow.AddDays(-4),
            UpdatedUtc = DateTime.UtcNow.AddDays(-2),
            DecisionUtc = DateTime.UtcNow.AddDays(-2)
        };

        var runStore = new FakeDiscoveryRunStore(run);
        var candidateStore = new FakeDiscoveryRunCandidateStore(candidate);
        var service = CreateService(runStore, candidateStore, options: new DiscoveryRunOperationsOptions
        {
            CandidateArchiveRetentionHours = 24,
            AbandonedHeartbeatTimeoutMinutes = 5,
            MaxRecoveryAttempts = 1
        });

        await service.SweepAsync(CancellationToken.None);
        var updatedCandidate = await candidateStore.GetAsync(run.RunId, candidate.CandidateKey, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(updatedCandidate, Is.Not.Null);
            Assert.That(updatedCandidate!.State, Is.EqualTo(DiscoveryRunCandidateStates.Archived));
            Assert.That(updatedCandidate.ArchiveReason, Is.EqualTo("retention_window_elapsed"));
            Assert.That(updatedCandidate.ArchivedUtc, Is.Not.Null);
            Assert.That(updatedCandidate.Revision, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task SweepAsync_SchedulesDueRecurringCampaignAndRefreshesCampaignMemory()
    {
        var completedRun = CreateRun("run_completed_campaign", DiscoveryRunStatuses.Completed);
        completedRun.RecurringCampaignId = "campaign_1";
        completedRun.CompletedUtc = DateTime.UtcNow.AddHours(-2);

        var acceptedCandidate = new DiscoveryRunCandidate
        {
            Id = "run_completed_campaign:safe_shop",
            RunId = completedRun.RunId,
            CandidateKey = "safe_shop",
            Revision = 2,
            State = DiscoveryRunCandidateStates.ManuallyAccepted,
            AcceptedSourceId = "safe_shop",
            DisplayName = "Safe Shop",
            BaseUrl = "https://safe.example/",
            Host = "safe.example",
            CandidateType = "retailer",
            AllowedMarkets = ["UK"],
            PreferredLocale = "en-GB",
            MatchedCategoryKeys = ["tv"],
            AllowedByGovernance = true,
            CreatedUtc = DateTime.UtcNow.AddDays(-2),
            UpdatedUtc = DateTime.UtcNow.AddHours(-2),
            DecisionUtc = DateTime.UtcNow.AddHours(-2)
        };

        var campaignStore = new FakeDiscoveryCampaignStore(new RecurringDiscoveryCampaign
        {
            CampaignId = "campaign_1",
            Name = "TV UK",
            CategoryKeys = ["tv"],
            Market = "UK",
            Locale = "en-GB",
            AutomationMode = SourceAutomationModes.SuggestAccept,
            Status = RecurringDiscoveryCampaignStatuses.Active,
            CampaignFingerprint = "market:uk|locale:en-gb|categories:tv|brands:",
            IntervalMinutes = 30,
            IntervalHours = 0,
            CreatedUtc = DateTime.UtcNow.AddDays(-3),
            UpdatedUtc = DateTime.UtcNow.AddDays(-3),
            NextScheduledUtc = DateTime.UtcNow.AddMinutes(-10)
        });

        var runStore = new FakeDiscoveryRunStore(completedRun);
        var candidateStore = new FakeDiscoveryRunCandidateStore(acceptedCandidate);
        var runService = new RecordingDiscoveryRunService();
        var service = CreateService(runStore, candidateStore, campaignStore, runService);

        await service.SweepAsync(CancellationToken.None);
        var campaign = await campaignStore.GetAsync("campaign_1", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(runService.ScheduledRuns, Has.Count.EqualTo(1));
            Assert.That(campaign, Is.Not.Null);
            Assert.That(campaign!.Memory.AcceptedCandidateCount, Is.EqualTo(1));
            Assert.That(campaign.Memory.HistoricalRunCount, Is.EqualTo(1));
            Assert.That(campaign.LastRunId, Is.EqualTo(runService.ScheduledRuns[0].RunId));
            Assert.That(campaign.NextScheduledUtc, Is.GreaterThan(DateTime.UtcNow.AddMinutes(25)));
        });
    }

    private static DiscoveryRunMaintenanceService CreateService(
        FakeDiscoveryRunStore runStore,
        FakeDiscoveryRunCandidateStore candidateStore,
        FakeDiscoveryCampaignStore? campaignStore = null,
        RecordingDiscoveryRunService? runService = null,
        DiscoveryRunOperationsOptions? options = null)
    {
        return new DiscoveryRunMaintenanceService(
            runStore,
            campaignStore ?? new FakeDiscoveryCampaignStore(),
            runService ?? new RecordingDiscoveryRunService(),
            candidateStore,
            Options.Create(options ?? new DiscoveryRunOperationsOptions
            {
                AbandonedHeartbeatTimeoutMinutes = 5,
                MaxRecoveryAttempts = 2,
                CandidateArchiveRetentionHours = 24
            }));
    }

    private static DiscoveryRun CreateRun(string runId, string status)
    {
        return new DiscoveryRun
        {
            RunId = runId,
            RequestedCategoryKeys = ["tv"],
            Market = "UK",
            Locale = "en-GB",
            AutomationMode = SourceAutomationModes.OperatorAssisted,
            Status = status,
            CurrentStage = DiscoveryRunStageNames.Publish,
            LlmStatus = "disabled",
            LlmStatusMessage = "Disabled.",
            CreatedUtc = DateTime.UtcNow.AddDays(-3),
            UpdatedUtc = DateTime.UtcNow.AddDays(-3)
        };
    }

    private sealed class FakeDiscoveryRunStore(params DiscoveryRun[] runs) : IDiscoveryRunStore
    {
        private readonly Dictionary<string, DiscoveryRun> items = runs.ToDictionary(run => run.RunId, StringComparer.OrdinalIgnoreCase);

        public Task<DiscoveryRunPage> ListAsync(DiscoveryRunQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(new DiscoveryRunPage { Items = items.Values.ToArray(), Page = query.Page, PageSize = query.PageSize, TotalCount = items.Count });

        public Task<DiscoveryRun?> GetAsync(string runId, CancellationToken cancellationToken = default)
            => Task.FromResult(items.TryGetValue(runId, out var run) ? run : null);

        public Task<DiscoveryRun?> GetNextQueuedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<DiscoveryRun?>(null);

        public Task<IReadOnlyList<DiscoveryRun>> ListByStatusesAsync(IReadOnlyCollection<string> statuses, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DiscoveryRun>>(items.Values.Where(run => statuses.Contains(run.Status)).ToArray());

        public Task<IReadOnlyList<DiscoveryRun>> ListByCampaignAsync(string campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DiscoveryRun>>(items.Values.Where(run => string.Equals(run.RecurringCampaignId, campaignId, StringComparison.OrdinalIgnoreCase)).OrderByDescending(run => run.CreatedUtc).ToArray());

        public Task<bool> HasIncompleteCampaignRunAsync(string campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult(items.Values.Any(run => string.Equals(run.RecurringCampaignId, campaignId, StringComparison.OrdinalIgnoreCase)
                && run.Status != DiscoveryRunStatuses.Completed
                && run.Status != DiscoveryRunStatuses.Cancelled
                && run.Status != DiscoveryRunStatuses.Failed));

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

        public Task<IReadOnlyList<DiscoveryRunCandidate>> ListByHostsAsync(IReadOnlyCollection<string> hosts, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DiscoveryRunCandidate>>(items.Values.Where(candidate => hosts.Contains(candidate.Host)).ToArray());

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

    private sealed class FakeDiscoveryCampaignStore(params RecurringDiscoveryCampaign[] campaigns) : IDiscoveryCampaignStore
    {
        private readonly Dictionary<string, RecurringDiscoveryCampaign> items = campaigns.ToDictionary(campaign => campaign.CampaignId, StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<RecurringDiscoveryCampaign>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RecurringDiscoveryCampaign>>(items.Values.OrderBy(campaign => campaign.Name).ToArray());

        public Task<IReadOnlyList<RecurringDiscoveryCampaign>> ListByStatusesAsync(IReadOnlyCollection<string> statuses, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RecurringDiscoveryCampaign>>(items.Values.Where(campaign => statuses.Contains(campaign.Status)).ToArray());

        public Task<IReadOnlyList<RecurringDiscoveryCampaign>> ListDueAsync(DateTime utcNow, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RecurringDiscoveryCampaign>>(items.Values
                .Where(campaign => string.Equals(campaign.Status, RecurringDiscoveryCampaignStatuses.Active, StringComparison.OrdinalIgnoreCase)
                    && campaign.NextScheduledUtc is not null
                    && campaign.NextScheduledUtc <= utcNow)
                .OrderBy(campaign => campaign.NextScheduledUtc)
                .Take(limit)
                .ToArray());

        public Task<RecurringDiscoveryCampaign?> GetAsync(string campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult(items.TryGetValue(campaignId, out var campaign) ? campaign : null);

        public Task<RecurringDiscoveryCampaign?> GetByFingerprintAsync(string campaignFingerprint, CancellationToken cancellationToken = default)
            => Task.FromResult(items.Values.FirstOrDefault(campaign => string.Equals(campaign.CampaignFingerprint, campaignFingerprint, StringComparison.OrdinalIgnoreCase)));

        public Task UpsertAsync(RecurringDiscoveryCampaign campaign, CancellationToken cancellationToken = default)
        {
            items[campaign.CampaignId] = campaign;
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(string campaignId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(items.Remove(campaignId));
        }
    }

    private sealed class RecordingDiscoveryRunService : IDiscoveryRunService
    {
        public List<DiscoveryRun> ScheduledRuns { get; } = [];

        public Task<DiscoveryRun> CreateAsync(CreateDiscoveryRunRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DiscoveryRun> CreateScheduledAsync(RecurringDiscoveryCampaign campaign, CancellationToken cancellationToken = default)
        {
            var run = new DiscoveryRun
            {
                RunId = $"scheduled_{ScheduledRuns.Count + 1}",
                TriggerKind = DiscoveryRunTriggerKinds.RecurringCampaign,
                RecurringCampaignId = campaign.CampaignId,
                RequestedCategoryKeys = campaign.CategoryKeys,
                Market = campaign.Market,
                Locale = campaign.Locale,
                AutomationMode = campaign.AutomationMode,
                BrandHints = campaign.BrandHints,
                Status = DiscoveryRunStatuses.Queued,
                CurrentStage = DiscoveryRunStageNames.Search,
                LlmStatus = "disabled",
                LlmStatusMessage = "Disabled.",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };
            ScheduledRuns.Add(run);
            return Task.FromResult(run);
        }

        public Task<DiscoveryRunPage> ListAsync(DiscoveryRunQuery query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DiscoveryRun?> GetAsync(string runId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<DiscoveryRunCandidate>> ListCandidatesAsync(string runId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DiscoveryRunCandidatePage> QueryCandidatesAsync(string runId, DiscoveryRunCandidateQuery query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DiscoveryRun?> PauseAsync(string runId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DiscoveryRun?> ResumeAsync(string runId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DiscoveryRun?> StopAsync(string runId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DiscoveryRunCandidate?> AcceptCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DiscoveryRunCandidate?> DismissCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DiscoveryRunCandidate?> RestoreCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
