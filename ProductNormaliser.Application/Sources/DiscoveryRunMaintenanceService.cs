using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProductNormaliser.Application.Observability;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

public sealed class DiscoveryRunMaintenanceService(
    IDiscoveryRunStore discoveryRunStore,
    IDiscoveryCampaignStore discoveryCampaignStore,
    IDiscoveryRunService discoveryRunService,
    IDiscoveryRunCandidateStore discoveryRunCandidateStore,
    IOptions<DiscoveryRunOperationsOptions> options,
    ILogger<DiscoveryRunMaintenanceService>? logger = null)
{
    private readonly DiscoveryRunOperationsOptions options = options.Value;
    private readonly ILogger<DiscoveryRunMaintenanceService> logger = logger ?? NullLogger<DiscoveryRunMaintenanceService>.Instance;

    public async Task SweepAsync(CancellationToken cancellationToken = default)
    {
        await RecoverAbandonedRunsAsync(cancellationToken);
        await ArchiveExpiredCandidatesAsync(cancellationToken);
        await RefreshRecurringCampaignsAsync(cancellationToken);
        await ScheduleDueCampaignsAsync(cancellationToken);
    }

    private async Task RefreshRecurringCampaignsAsync(CancellationToken cancellationToken)
    {
        var campaigns = await discoveryCampaignStore.ListAsync(cancellationToken);
        foreach (var campaign in campaigns)
        {
            var runs = await discoveryRunStore.ListByCampaignAsync(campaign.CampaignId, cancellationToken);
            campaign.Memory = await BuildCampaignMemoryAsync(runs, cancellationToken);
            campaign.LastRunId = runs.FirstOrDefault()?.RunId;

            if (campaign.NextScheduledUtc is null && string.Equals(campaign.Status, RecurringDiscoveryCampaignStatuses.Active, StringComparison.OrdinalIgnoreCase))
            {
                campaign.NextScheduledUtc = DateTime.UtcNow;
            }

            campaign.StatusMessage = BuildCampaignStatusMessage(campaign, await discoveryRunStore.HasIncompleteCampaignRunAsync(campaign.CampaignId, cancellationToken));
            campaign.UpdatedUtc = DateTime.UtcNow;
            await discoveryCampaignStore.UpsertAsync(campaign, cancellationToken);
        }
    }

    private async Task ScheduleDueCampaignsAsync(CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var dueCampaigns = await discoveryCampaignStore.ListDueAsync(utcNow, options.RecurringCampaignSweepBatchSize, cancellationToken);
        foreach (var campaign in dueCampaigns)
        {
            if (await discoveryRunStore.HasIncompleteCampaignRunAsync(campaign.CampaignId, cancellationToken))
            {
                campaign.StatusMessage = "Recurring discovery campaign is waiting for the current scheduled run to finish before queuing another run.";
                campaign.UpdatedUtc = utcNow;
                await discoveryCampaignStore.UpsertAsync(campaign, cancellationToken);
                continue;
            }

            var scheduledRun = await discoveryRunService.CreateScheduledAsync(campaign, cancellationToken);
            campaign.LastRunId = scheduledRun.RunId;
            campaign.LastScheduledUtc = utcNow;
            campaign.NextScheduledUtc = utcNow.AddHours(Math.Max(1, campaign.IntervalHours));
            campaign.StatusMessage = BuildCampaignStatusMessage(campaign, hasIncompleteRun: true);
            campaign.UpdatedUtc = utcNow;
            await discoveryCampaignStore.UpsertAsync(campaign, cancellationToken);
            logger.LogInformation("Scheduled recurring discovery campaign {CampaignId} as run {RunId}.", campaign.CampaignId, scheduledRun.RunId);
        }
    }

    private async Task RecoverAbandonedRunsAsync(CancellationToken cancellationToken)
    {
        var staleCutoffUtc = DateTime.UtcNow.AddMinutes(-Math.Max(1, options.AbandonedHeartbeatTimeoutMinutes));
        var activeRuns = await discoveryRunStore.ListByStatusesAsync([DiscoveryRunStatuses.Running, DiscoveryRunStatuses.CancelRequested], cancellationToken);
        foreach (var run in activeRuns)
        {
            var lastSeenUtc = run.LastHeartbeatUtc ?? run.UpdatedUtc;
            if (lastSeenUtc >= staleCutoffUtc)
            {
                continue;
            }

            if (string.Equals(run.Status, DiscoveryRunStatuses.CancelRequested, StringComparison.OrdinalIgnoreCase))
            {
                run.Status = DiscoveryRunStatuses.Cancelled;
                run.CompletedUtc = DateTime.UtcNow;
                run.UpdatedUtc = run.CompletedUtc.Value;
                run.LastHeartbeatUtc = run.CompletedUtc.Value;
                run.StatusMessage = "Discovery run was cancelled by the maintenance sweeper after the worker stopped heartbeating.";
                AppendDiagnostic(run, "run_cancelled_after_restart", SourceCandidateDiscoveryDiagnostic.SeverityWarning, "Cancellation completed after restart", "The maintenance sweeper completed a pending cancellation after the worker stopped heartbeating.");
                await discoveryRunStore.UpsertAsync(run, cancellationToken);
                continue;
            }

            if (run.RecoveryAttemptCount < Math.Max(1, options.MaxRecoveryAttempts))
            {
                run.RecoveryAttemptCount += 1;
                run.Status = DiscoveryRunStatuses.Queued;
                run.UpdatedUtc = DateTime.UtcNow;
                run.LastHeartbeatUtc = run.UpdatedUtc;
                run.StatusMessage = "Discovery run stopped heartbeating and was re-queued for deterministic recovery.";
                AppendDiagnostic(run, "run_recovered_after_restart", SourceCandidateDiscoveryDiagnostic.SeverityWarning, "Run recovered after restart", $"The maintenance sweeper detected an abandoned run and re-queued it for retry attempt {run.RecoveryAttemptCount}.");
                await discoveryRunStore.UpsertAsync(run, cancellationToken);
                ProductNormaliserTelemetry.DiscoveryRunsRecovered.Add(1, new KeyValuePair<string, object?>("attempt", run.RecoveryAttemptCount));
                continue;
            }

            run.Status = DiscoveryRunStatuses.Failed;
            run.FailureMessage = "Discovery run exhausted deterministic recovery attempts after the worker stopped heartbeating.";
            run.CompletedUtc = DateTime.UtcNow;
            run.UpdatedUtc = run.CompletedUtc.Value;
            run.LastHeartbeatUtc = run.CompletedUtc.Value;
            run.StatusMessage = "Discovery run failed after exhausting recovery attempts.";
            AppendDiagnostic(run, "run_recovery_exhausted", SourceCandidateDiscoveryDiagnostic.SeverityError, "Run recovery exhausted", "The maintenance sweeper detected an abandoned run that had already consumed its allowed recovery attempts and marked it failed.");
            await discoveryRunStore.UpsertAsync(run, cancellationToken);
            ProductNormaliserTelemetry.DiscoveryRunsFailedRecovery.Add(1, new KeyValuePair<string, object?>("attempt", run.RecoveryAttemptCount));
        }
    }

    private async Task ArchiveExpiredCandidatesAsync(CancellationToken cancellationToken)
    {
        var archiveCutoffUtc = DateTime.UtcNow.AddHours(-Math.Max(1, options.CandidateArchiveRetentionHours));
        var terminalRuns = await discoveryRunStore.ListByStatusesAsync([DiscoveryRunStatuses.Completed, DiscoveryRunStatuses.Cancelled, DiscoveryRunStatuses.Failed], cancellationToken);
        foreach (var run in terminalRuns)
        {
            var candidates = await discoveryRunCandidateStore.ListByRunAsync(run.RunId, cancellationToken);
            var changed = false;
            foreach (var candidate in candidates)
            {
                if (!IsEligibleForArchive(candidate, archiveCutoffUtc))
                {
                    continue;
                }

                var archivedCandidate = CloneCandidate(candidate);
                archivedCandidate.PreviousState = candidate.State;
                archivedCandidate.State = DiscoveryRunCandidateStates.Archived;
                archivedCandidate.StateMessage = $"Archived automatically after the {options.CandidateArchiveRetentionHours}-hour retention window elapsed.";
                archivedCandidate.ArchiveReason = "retention_window_elapsed";
                archivedCandidate.ArchivedUtc = DateTime.UtcNow;
                archivedCandidate.UpdatedUtc = archivedCandidate.ArchivedUtc.Value;
                archivedCandidate.Revision = candidate.Revision + 1;

                if (!await discoveryRunCandidateStore.TryUpdateAsync(archivedCandidate, candidate.Revision, cancellationToken))
                {
                    continue;
                }

                ProductNormaliserTelemetry.DiscoveryCandidatesArchived.Add(1, new KeyValuePair<string, object?>("reason", archivedCandidate.ArchiveReason));
                changed = true;
            }

            if (changed)
            {
                await RefreshRunSummaryAsync(run, cancellationToken);
            }
        }
    }

    private async Task RefreshRunSummaryAsync(DiscoveryRun run, CancellationToken cancellationToken)
    {
        var candidates = await discoveryRunCandidateStore.ListByRunAsync(run.RunId, cancellationToken);
        run.SuggestedCandidateCount = candidates.Count(candidate => string.Equals(candidate.State, DiscoveryRunCandidateStates.Suggested, StringComparison.OrdinalIgnoreCase));
        run.AutoAcceptedCandidateCount = Math.Max(
            run.AutoAcceptedCandidateCount,
            candidates.Count(candidate => string.Equals(candidate.State, DiscoveryRunCandidateStates.AutoAccepted, StringComparison.OrdinalIgnoreCase)));
        run.PublishedCandidateCount = candidates.Count(candidate => !string.IsNullOrWhiteSpace(candidate.AcceptedSourceId));
        run.UpdatedUtc = DateTime.UtcNow;
        await discoveryRunStore.UpsertAsync(run, cancellationToken);
    }

    private static bool IsEligibleForArchive(DiscoveryRunCandidate candidate, DateTime archiveCutoffUtc)
    {
        if (string.Equals(candidate.State, DiscoveryRunCandidateStates.Archived, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var candidateTimestamp = candidate.DecisionUtc ?? candidate.UpdatedUtc;
        if (candidateTimestamp > archiveCutoffUtc)
        {
            return false;
        }

        return string.Equals(candidate.State, DiscoveryRunCandidateStates.AutoAccepted, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.State, DiscoveryRunCandidateStates.ManuallyAccepted, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.State, DiscoveryRunCandidateStates.Superseded, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<RecurringDiscoveryCampaignMemory> BuildCampaignMemoryAsync(IReadOnlyList<DiscoveryRun> runs, CancellationToken cancellationToken)
    {
        var acceptedCandidateCount = 0;
        var dismissedCandidateCount = 0;
        var supersededCandidateCount = 0;
        var archivedCandidateCount = 0;
        var runsWithAcceptedCandidates = 0;
        DateTime? lastAcceptedUtc = null;

        foreach (var run in runs)
        {
            var candidates = await discoveryRunCandidateStore.ListByRunAsync(run.RunId, cancellationToken);
            var acceptedInRun = false;
            foreach (var candidate in candidates)
            {
                if (IsAcceptedCandidate(candidate))
                {
                    acceptedCandidateCount += 1;
                    acceptedInRun = true;
                    var acceptedUtc = candidate.DecisionUtc ?? candidate.UpdatedUtc;
                    if (lastAcceptedUtc is null || acceptedUtc > lastAcceptedUtc.Value)
                    {
                        lastAcceptedUtc = acceptedUtc;
                    }
                }

                if (string.Equals(candidate.State, DiscoveryRunCandidateStates.Dismissed, StringComparison.OrdinalIgnoreCase))
                {
                    dismissedCandidateCount += 1;
                }

                if (string.Equals(candidate.State, DiscoveryRunCandidateStates.Superseded, StringComparison.OrdinalIgnoreCase))
                {
                    supersededCandidateCount += 1;
                }

                if (string.Equals(candidate.State, DiscoveryRunCandidateStates.Archived, StringComparison.OrdinalIgnoreCase))
                {
                    archivedCandidateCount += 1;
                }
            }

            if (acceptedInRun)
            {
                runsWithAcceptedCandidates += 1;
            }
        }

        var completedRuns = runs.Count(IsTerminalRun);
        return new RecurringDiscoveryCampaignMemory
        {
            HistoricalRunCount = runs.Count,
            CompletedRunCount = completedRuns,
            AcceptedCandidateCount = acceptedCandidateCount,
            DismissedCandidateCount = dismissedCandidateCount,
            SupersededCandidateCount = supersededCandidateCount,
            ArchivedCandidateCount = archivedCandidateCount,
            RunsWithAcceptedCandidates = runsWithAcceptedCandidates,
            RunsWithoutAcceptedCandidates = Math.Max(0, completedRuns - runsWithAcceptedCandidates),
            LastCompletedUtc = runs.Where(run => run.CompletedUtc is not null).Select(run => run.CompletedUtc).Max(),
            LastAcceptedUtc = lastAcceptedUtc
        };
    }

    private static string BuildCampaignStatusMessage(RecurringDiscoveryCampaign campaign, bool hasIncompleteRun)
    {
        if (string.Equals(campaign.Status, RecurringDiscoveryCampaignStatuses.Paused, StringComparison.OrdinalIgnoreCase))
        {
            return "Recurring discovery campaign is paused and will not schedule new runs.";
        }

        if (hasIncompleteRun)
        {
            return "Recurring discovery campaign has an active scheduled run in progress.";
        }

        if (campaign.Memory.AcceptedCandidateCount > 0)
        {
            return $"Recurring discovery campaign has produced {campaign.Memory.AcceptedCandidateCount} accepted candidates across {campaign.Memory.HistoricalRunCount} runs.";
        }

        if (campaign.Memory.HistoricalRunCount > 0)
        {
            return $"Recurring discovery campaign has completed {campaign.Memory.HistoricalRunCount} runs and is still building source memory for this scope.";
        }

        return "Recurring discovery campaign is active and waiting for the next maintenance sweep.";
    }

    private static bool IsAcceptedCandidate(DiscoveryRunCandidate candidate)
    {
        return !string.IsNullOrWhiteSpace(candidate.AcceptedSourceId)
            || string.Equals(candidate.State, DiscoveryRunCandidateStates.AutoAccepted, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.State, DiscoveryRunCandidateStates.ManuallyAccepted, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTerminalRun(DiscoveryRun run)
    {
        return string.Equals(run.Status, DiscoveryRunStatuses.Completed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(run.Status, DiscoveryRunStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)
            || string.Equals(run.Status, DiscoveryRunStatuses.Failed, StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendDiagnostic(DiscoveryRun run, string code, string severity, string title, string message)
    {
        run.Diagnostics ??= [];
        if (run.Diagnostics.Any(existing => string.Equals(existing.Code, code, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        run.Diagnostics.Add(new DiscoveryRunDiagnostic
        {
            Code = code,
            Severity = severity,
            Title = title,
            Message = message
        });
    }

    private static DiscoveryRunCandidate CloneCandidate(DiscoveryRunCandidate candidate)
    {
        return new DiscoveryRunCandidate
        {
            Id = candidate.Id,
            RunId = candidate.RunId,
            CandidateKey = candidate.CandidateKey,
            Revision = candidate.Revision,
            State = candidate.State,
            PreviousState = candidate.PreviousState,
            SupersededByCandidateKey = candidate.SupersededByCandidateKey,
            SuppressionDispositionId = candidate.SuppressionDispositionId,
            AcceptedSourceId = candidate.AcceptedSourceId,
            StateMessage = candidate.StateMessage,
            ArchiveReason = candidate.ArchiveReason,
            DisplayName = candidate.DisplayName,
            BaseUrl = candidate.BaseUrl,
            Host = candidate.Host,
            CandidateType = candidate.CandidateType,
            AllowedMarkets = candidate.AllowedMarkets.ToArray(),
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
            MatchedCategoryKeys = candidate.MatchedCategoryKeys.ToArray(),
            MatchedBrandHints = candidate.MatchedBrandHints.ToArray(),
            AlreadyRegistered = candidate.AlreadyRegistered,
            DuplicateSourceIds = candidate.DuplicateSourceIds.ToArray(),
            DuplicateSourceDisplayNames = candidate.DuplicateSourceDisplayNames.ToArray(),
            AllowedByGovernance = candidate.AllowedByGovernance,
            GovernanceWarning = candidate.GovernanceWarning,
            Probe = candidate.Probe,
            AutomationAssessment = candidate.AutomationAssessment,
            Reasons = candidate.Reasons.ToList(),
            CreatedUtc = candidate.CreatedUtc,
            UpdatedUtc = candidate.UpdatedUtc,
            DecisionUtc = candidate.DecisionUtc,
            ArchivedUtc = candidate.ArchivedUtc
        };
    }
}