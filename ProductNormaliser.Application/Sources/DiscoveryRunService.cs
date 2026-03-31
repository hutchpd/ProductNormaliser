using ProductNormaliser.Application.AI;
using ProductNormaliser.Application.Categories;
using ProductNormaliser.Application.Governance;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

public sealed class DiscoveryRunService(
    IDiscoveryRunStore discoveryRunStore,
    IDiscoveryRunCandidateStore discoveryRunCandidateStore,
    IDiscoveryRunCandidateDispositionStore discoveryRunCandidateDispositionStore,
    ICategoryMetadataService categoryMetadataService,
    ISourceManagementService sourceManagementService,
    IManagementAuditService managementAuditService,
    ILlmStatusProvider? llmStatusProvider = null) : IDiscoveryRunService
{
    public DiscoveryRunService(
        IDiscoveryRunStore discoveryRunStore,
        IDiscoveryRunCandidateStore discoveryRunCandidateStore,
        ICategoryMetadataService categoryMetadataService,
        ISourceManagementService sourceManagementService,
        IManagementAuditService managementAuditService,
        ILlmStatusProvider? llmStatusProvider = null)
        : this(
            discoveryRunStore,
            discoveryRunCandidateStore,
            NullDiscoveryRunCandidateDispositionStore.Instance,
            categoryMetadataService,
            sourceManagementService,
            managementAuditService,
            llmStatusProvider)
    {
    }

    public async Task<DiscoveryRun> CreateAsync(CreateDiscoveryRunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var categoryKeys = SourceCandidateDiscoveryEvaluator.NormalizeValues(request.CategoryKeys);
        if (categoryKeys.Count == 0)
        {
            throw new ArgumentException("Choose at least one category before starting a discovery run.", nameof(request));
        }

        var knownCategoryKeys = (await categoryMetadataService.ListAsync(enabledOnly: false, cancellationToken))
            .Select(category => category.CategoryKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknownCategoryKeys = categoryKeys
            .Where(categoryKey => !knownCategoryKeys.Contains(categoryKey))
            .ToArray();
        if (unknownCategoryKeys.Length > 0)
        {
            throw new ArgumentException($"Unknown category keys: {string.Join(", ", unknownCategoryKeys)}.", nameof(request));
        }

        var run = BuildQueuedRun(
            categoryKeys,
            SourceCandidateDiscoveryEvaluator.NormalizeOptionalText(request.Locale),
            SourceCandidateDiscoveryEvaluator.NormalizeOptionalText(request.Market),
            SourceAutomationModes.Normalize(request.AutomationMode),
            SourceCandidateDiscoveryEvaluator.NormalizeValues(request.BrandHints),
            SourceCandidateDiscoveryEvaluator.NormalizeMaxCandidates(request.MaxCandidates),
            DiscoveryRunTriggerKinds.Manual,
            recurringCampaignId: null,
            recurringCampaignFingerprint: null,
            "Discovery run is queued and waiting for worker capacity.");

        await discoveryRunStore.UpsertAsync(run, cancellationToken);
        await managementAuditService.RecordAsync(
            "discovery_run_created",
            "discovery_run",
            run.RunId,
            new Dictionary<string, string>
            {
                ["categories"] = string.Join(',', run.RequestedCategoryKeys),
                ["market"] = run.Market ?? string.Empty,
                ["locale"] = run.Locale ?? string.Empty,
                ["automationMode"] = run.AutomationMode
            },
            cancellationToken);

        return run;
    }

    public async Task<DiscoveryRun> CreateScheduledAsync(RecurringDiscoveryCampaign campaign, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        var run = BuildQueuedRun(
            campaign.CategoryKeys,
            campaign.Locale,
            campaign.Market,
            campaign.AutomationMode,
            campaign.BrandHints,
            campaign.MaxCandidatesPerRun,
            DiscoveryRunTriggerKinds.RecurringCampaign,
            campaign.CampaignId,
            campaign.CampaignFingerprint,
            $"Recurring discovery campaign '{campaign.Name}' queued a fresh run and is waiting for worker capacity.");

        await discoveryRunStore.UpsertAsync(run, cancellationToken);
        await managementAuditService.RecordAsync(
            "discovery_run_scheduled",
            "discovery_run",
            run.RunId,
            new Dictionary<string, string>
            {
                ["campaignId"] = campaign.CampaignId,
                ["categories"] = string.Join(',', run.RequestedCategoryKeys),
                ["market"] = run.Market ?? string.Empty,
                ["locale"] = run.Locale ?? string.Empty,
                ["automationMode"] = run.AutomationMode
            },
            cancellationToken);

        return run;
    }

    public Task<DiscoveryRunPage> ListAsync(DiscoveryRunQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return discoveryRunStore.ListAsync(query, cancellationToken);
    }

    public Task<DiscoveryRun?> GetAsync(string runId, CancellationToken cancellationToken = default)
    {
        return discoveryRunStore.GetAsync(NormalizeRequired(runId, nameof(runId)), cancellationToken);
    }

    public async Task<IReadOnlyList<DiscoveryRunCandidate>> ListCandidatesAsync(string runId, CancellationToken cancellationToken = default)
    {
        return await discoveryRunCandidateStore.ListByRunAsync(NormalizeRequired(runId, nameof(runId)), cancellationToken);
    }

    public Task<DiscoveryRunCandidatePage> QueryCandidatesAsync(string runId, DiscoveryRunCandidateQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return discoveryRunCandidateStore.QueryByRunAsync(
            NormalizeRequired(runId, nameof(runId)),
            new DiscoveryRunCandidateQuery
            {
                StateFilter = NormalizeCandidateStateFilter(query.StateFilter),
                Sort = NormalizeCandidateSort(query.Sort),
                Page = Math.Max(1, query.Page),
                PageSize = Math.Clamp(query.PageSize <= 0 ? 12 : query.PageSize, 1, 100)
            },
            cancellationToken);
    }

    public async Task<DiscoveryRun?> PauseAsync(string runId, CancellationToken cancellationToken = default)
    {
        var run = await discoveryRunStore.GetAsync(NormalizeRequired(runId, nameof(runId)), cancellationToken);
        if (run is null)
        {
            return null;
        }

        if (!DiscoveryRunStateMachine.CanPause(run.Status))
        {
            throw new InvalidOperationException($"Discovery run '{run.RunId}' cannot be paused from status '{run.Status}'.");
        }

        run.Status = DiscoveryRunStatuses.Paused;
        run.StatusMessage = "Discovery run is paused. Resume to continue background execution.";
        run.UpdatedUtc = DateTime.UtcNow;
        await discoveryRunStore.UpsertAsync(run, cancellationToken);
        return run;
    }

    public async Task<DiscoveryRun?> ResumeAsync(string runId, CancellationToken cancellationToken = default)
    {
        var run = await discoveryRunStore.GetAsync(NormalizeRequired(runId, nameof(runId)), cancellationToken);
        if (run is null)
        {
            return null;
        }

        if (!DiscoveryRunStateMachine.CanResume(run.Status))
        {
            throw new InvalidOperationException($"Discovery run '{run.RunId}' cannot be resumed from status '{run.Status}'.");
        }

        run.Status = DiscoveryRunStatuses.Queued;
        run.StatusMessage = "Discovery run was resumed and re-queued for worker execution.";
        run.UpdatedUtc = DateTime.UtcNow;
        await discoveryRunStore.UpsertAsync(run, cancellationToken);
        return run;
    }

    public async Task<DiscoveryRun?> StopAsync(string runId, CancellationToken cancellationToken = default)
    {
        var run = await discoveryRunStore.GetAsync(NormalizeRequired(runId, nameof(runId)), cancellationToken);
        if (run is null)
        {
            return null;
        }

        if (DiscoveryRunStateMachine.CanStopImmediately(run.Status))
        {
            run.Status = DiscoveryRunStatuses.Cancelled;
            run.CompletedUtc = DateTime.UtcNow;
            run.StatusMessage = "Discovery run was cancelled before more background work started.";
        }
        else if (DiscoveryRunStateMachine.CanRequestStop(run.Status))
        {
            run.Status = DiscoveryRunStatuses.CancelRequested;
            run.CancelRequestedUtc = DateTime.UtcNow;
            run.StatusMessage = "Cancellation was requested. The worker will stop after the current unit of work finishes.";
        }
        else if (DiscoveryRunStateMachine.IsTerminal(run.Status))
        {
            throw new InvalidOperationException($"Discovery run '{run.RunId}' is already terminal with status '{run.Status}'.");
        }

        run.UpdatedUtc = DateTime.UtcNow;
        await discoveryRunStore.UpsertAsync(run, cancellationToken);
        return run;
    }

    public async Task<DiscoveryRunCandidate?> AcceptCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default)
    {
        var run = await RequireRunAsync(runId, cancellationToken);
        var candidate = await discoveryRunCandidateStore.GetAsync(run.RunId, NormalizeRequired(candidateKey, nameof(candidateKey)), cancellationToken);
        if (candidate is null)
        {
            return null;
        }

        if (!DiscoveryRunStateMachine.CanAcceptCandidate(candidate.State))
        {
            throw new InvalidOperationException($"Candidate '{candidate.CandidateKey}' cannot be accepted from state '{candidate.State}'.");
        }

        var claimedCandidate = CloneCandidate(candidate);
        claimedCandidate.PreviousState = candidate.State;
        claimedCandidate.State = DiscoveryRunCandidateStates.ManuallyAccepted;
        claimedCandidate.StateMessage = "Accepted by operator. Publishing source registration.";
        claimedCandidate.DecisionUtc = DateTime.UtcNow;
        claimedCandidate.UpdatedUtc = claimedCandidate.DecisionUtc.Value;
        claimedCandidate.Revision = GetNextRevision(claimedCandidate.Revision, expectedRevision);
        claimedCandidate.SupersededByCandidateKey = null;

        if (!await discoveryRunCandidateStore.TryUpdateAsync(claimedCandidate, expectedRevision, cancellationToken))
        {
            throw CreateConcurrencyException(candidate.CandidateKey);
        }

        var registration = new CrawlSourceRegistration
        {
            SourceId = claimedCandidate.CandidateKey,
            DisplayName = claimedCandidate.DisplayName,
            BaseUrl = claimedCandidate.BaseUrl,
            AllowedMarkets = claimedCandidate.AllowedMarkets.Count == 0 && !string.IsNullOrWhiteSpace(run.Market)
                ? [run.Market]
                : claimedCandidate.AllowedMarkets,
            PreferredLocale = claimedCandidate.PreferredLocale ?? run.Locale,
            AutomationPolicy = new SourceAutomationPolicy { Mode = run.AutomationMode },
            SupportedCategoryKeys = claimedCandidate.MatchedCategoryKeys.Count == 0 ? run.RequestedCategoryKeys : claimedCandidate.MatchedCategoryKeys,
            IsEnabled = true
        };

        try
        {
            var source = await sourceManagementService.RegisterAsync(registration, cancellationToken);
            return await CompleteAcceptedCandidateAsync(
                run,
                claimedCandidate,
                source,
                linkedToExistingSource: false,
                $"Accepted and registered as source '{source.Id}'.",
                cancellationToken);
        }
        catch (ArgumentException exception) when (IsDuplicateSourceRegistrationException(exception, registration.SourceId))
        {
            var existingSource = await sourceManagementService.GetAsync(registration.SourceId, cancellationToken);
            if (existingSource is not null)
            {
                return await CompleteAcceptedCandidateAsync(
                    run,
                    claimedCandidate,
                    existingSource,
                    linkedToExistingSource: true,
                    $"Accepted and linked to existing source '{existingSource.Id}' because it was already registered.",
                    cancellationToken);
            }

            await RevertAcceptedCandidateAsync(run, claimedCandidate, cancellationToken);
            throw;
        }
        catch
        {
            await RevertAcceptedCandidateAsync(run, claimedCandidate, cancellationToken);
            throw;
        }
    }

    public async Task<DiscoveryRunCandidate?> DismissCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default)
    {
        var run = await RequireRunAsync(runId, cancellationToken);
        var candidate = await discoveryRunCandidateStore.GetAsync(NormalizeRequired(runId, nameof(runId)), NormalizeRequired(candidateKey, nameof(candidateKey)), cancellationToken);
        if (candidate is null)
        {
            return null;
        }

        if (!DiscoveryRunStateMachine.CanDismissCandidate(candidate.State))
        {
            throw new InvalidOperationException($"Candidate '{candidate.CandidateKey}' cannot be dismissed from state '{candidate.State}'.");
        }

        var updatedCandidate = CloneCandidate(candidate);
        updatedCandidate.PreviousState = candidate.State;
        updatedCandidate.State = DiscoveryRunCandidateStates.Dismissed;
        updatedCandidate.StateMessage = "Dismissed by operator.";
        updatedCandidate.DecisionUtc = DateTime.UtcNow;
        updatedCandidate.UpdatedUtc = updatedCandidate.DecisionUtc.Value;
        updatedCandidate.ArchiveReason = null;
        updatedCandidate.ArchivedUtc = null;
        updatedCandidate.SuppressionDispositionId = null;
        updatedCandidate.Revision = GetNextRevision(updatedCandidate.Revision, expectedRevision);

        if (!await discoveryRunCandidateStore.TryUpdateAsync(updatedCandidate, expectedRevision, cancellationToken))
        {
            throw CreateConcurrencyException(candidate.CandidateKey);
        }

        await UpsertDispositionAsync(run, updatedCandidate, DiscoveryRunCandidateStates.Dismissed, supersededByCandidateKey: null, cancellationToken);
        await RefreshRunSummaryAsync(run, cancellationToken);
        return updatedCandidate;
    }

    public async Task<DiscoveryRunCandidate?> RestoreCandidateAsync(string runId, string candidateKey, int expectedRevision, CancellationToken cancellationToken = default)
    {
        var run = await RequireRunAsync(runId, cancellationToken);
        var candidate = await discoveryRunCandidateStore.GetAsync(NormalizeRequired(runId, nameof(runId)), NormalizeRequired(candidateKey, nameof(candidateKey)), cancellationToken);
        if (candidate is null)
        {
            return null;
        }

        if (!DiscoveryRunStateMachine.CanRestoreCandidate(candidate.State))
        {
            throw new InvalidOperationException($"Candidate '{candidate.CandidateKey}' cannot be restored from state '{candidate.State}'.");
        }

        var updatedCandidate = CloneCandidate(candidate);
        updatedCandidate.State = DetermineRestoreState(candidate);
        updatedCandidate.StateMessage = "Restored to the active candidate queue.";
        updatedCandidate.SupersededByCandidateKey = null;
        updatedCandidate.SuppressionDispositionId = null;
        updatedCandidate.ArchiveReason = null;
        updatedCandidate.ArchivedUtc = null;
        updatedCandidate.UpdatedUtc = DateTime.UtcNow;
        updatedCandidate.Revision = GetNextRevision(updatedCandidate.Revision, expectedRevision);

        if (!await discoveryRunCandidateStore.TryUpdateAsync(updatedCandidate, expectedRevision, cancellationToken))
        {
            throw CreateConcurrencyException(candidate.CandidateKey);
        }

        await DeactivateMatchingDispositionsAsync(run, candidate, cancellationToken);
        await RefreshRunSummaryAsync(run, cancellationToken);
        return updatedCandidate;
    }

    private async Task RefreshRunSummaryAsync(DiscoveryRun run, CancellationToken cancellationToken)
    {
        var candidates = await discoveryRunCandidateStore.ListByRunAsync(run.RunId, cancellationToken);
        run.SuggestedCandidateCount = candidates.Count(candidate => string.Equals(candidate.State, DiscoveryRunCandidateStates.Suggested, StringComparison.OrdinalIgnoreCase));
        run.AutoAcceptedCandidateCount = Math.Max(
            run.AutoAcceptedCandidateCount,
            candidates.Count(candidate => string.Equals(candidate.State, DiscoveryRunCandidateStates.AutoAccepted, StringComparison.OrdinalIgnoreCase)));
        run.PublishedCandidateCount = candidates.Count(candidate => !string.IsNullOrWhiteSpace(candidate.AcceptedSourceId));
        var processedCandidates = candidates.Count(candidate => !string.Equals(candidate.State, DiscoveryRunCandidateStates.Pending, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate.State, DiscoveryRunCandidateStates.Probing, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate.State, DiscoveryRunCandidateStates.AwaitingLlm, StringComparison.OrdinalIgnoreCase));
        var manualReviewCandidates = candidates.Count(candidate => string.Equals(candidate.State, DiscoveryRunCandidateStates.Suggested, StringComparison.OrdinalIgnoreCase));
        var acceptedCandidates = candidates.Count(candidate => !string.IsNullOrWhiteSpace(candidate.AcceptedSourceId)
            || string.Equals(candidate.State, DiscoveryRunCandidateStates.AutoAccepted, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.State, DiscoveryRunCandidateStates.ManuallyAccepted, StringComparison.OrdinalIgnoreCase));
        if (processedCandidates > 0)
        {
            run.AcceptanceRate = decimal.Round(acceptedCandidates / (decimal)processedCandidates, 4, MidpointRounding.AwayFromZero);
            run.ManualReviewRate = decimal.Round(manualReviewCandidates / (decimal)processedCandidates, 4, MidpointRounding.AwayFromZero);
        }

        var firstAcceptedUtc = candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.AcceptedSourceId) && candidate.DecisionUtc is not null)
            .OrderBy(candidate => candidate.DecisionUtc)
            .Select(candidate => candidate.DecisionUtc)
            .FirstOrDefault();
        run.FirstAcceptedUtc = firstAcceptedUtc;
        if (run.StartedUtc is not null && firstAcceptedUtc is not null)
        {
            run.TimeToFirstAcceptedCandidateMs = Math.Max(0L, (long)(firstAcceptedUtc.Value - run.StartedUtc.Value).TotalMilliseconds);
        }

        if (run.StartedUtc is not null)
        {
            var elapsedMinutes = Math.Max(1d / 60d, Math.Max(0d, (DateTime.UtcNow - run.StartedUtc.Value).TotalMinutes));
            run.CandidateThroughputPerMinute = decimal.Round((decimal)(run.ProbeCompletedCount / elapsedMinutes), 4, MidpointRounding.AwayFromZero);
        }

        run.UpdatedUtc = DateTime.UtcNow;
        await discoveryRunStore.UpsertAsync(run, cancellationToken);
    }

    private async Task<DiscoveryRunCandidate> CompleteAcceptedCandidateAsync(
        DiscoveryRun run,
        DiscoveryRunCandidate claimedCandidate,
        CrawlSource source,
        bool linkedToExistingSource,
        string stateMessage,
        CancellationToken cancellationToken)
    {
        claimedCandidate.AcceptedSourceId = source.Id;
        if (linkedToExistingSource)
        {
            claimedCandidate.AlreadyRegistered = true;
            claimedCandidate.DuplicateSourceIds = claimedCandidate.DuplicateSourceIds
                .Append(source.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            claimedCandidate.DuplicateSourceDisplayNames = claimedCandidate.DuplicateSourceDisplayNames
                .Append(source.DisplayName)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        claimedCandidate.StateMessage = stateMessage;
        claimedCandidate.UpdatedUtc = DateTime.UtcNow;
        claimedCandidate.DecisionUtc = claimedCandidate.UpdatedUtc;
        claimedCandidate.Revision += 1;
        await discoveryRunCandidateStore.UpsertAsync(claimedCandidate, cancellationToken);

        await SupersedeDuplicateCandidatesAsync(run, claimedCandidate, cancellationToken);
        await RefreshRunSummaryAsync(run, cancellationToken);
        return claimedCandidate;
    }

    private async Task RevertAcceptedCandidateAsync(DiscoveryRun run, DiscoveryRunCandidate claimedCandidate, CancellationToken cancellationToken)
    {
        claimedCandidate.State = claimedCandidate.PreviousState ?? DiscoveryRunCandidateStates.Suggested;
        claimedCandidate.StateMessage = "Acceptance failed before source registration completed. The candidate was returned to review.";
        claimedCandidate.AcceptedSourceId = null;
        claimedCandidate.UpdatedUtc = DateTime.UtcNow;
        claimedCandidate.Revision += 1;
        await discoveryRunCandidateStore.UpsertAsync(claimedCandidate, cancellationToken);
        await RefreshRunSummaryAsync(run, cancellationToken);
    }

    private async Task SupersedeDuplicateCandidatesAsync(DiscoveryRun run, DiscoveryRunCandidate acceptedCandidate, CancellationToken cancellationToken)
    {
        var candidates = await discoveryRunCandidateStore.ListByRunAsync(run.RunId, cancellationToken);
        foreach (var candidate in candidates)
        {
            if (!DiscoveryRunStateMachine.CanSupersedeCandidate(candidate.State)
                || !DiscoveryRunCandidateComparer.ArePotentialDuplicates(acceptedCandidate, candidate))
            {
                continue;
            }

            var supersededCandidate = CloneCandidate(candidate);
            supersededCandidate.PreviousState = candidate.State;
            supersededCandidate.State = DiscoveryRunCandidateStates.Superseded;
            supersededCandidate.SupersededByCandidateKey = acceptedCandidate.CandidateKey;
            supersededCandidate.StateMessage = $"Superseded by accepted candidate '{acceptedCandidate.DisplayName}'.";
            supersededCandidate.DecisionUtc = DateTime.UtcNow;
            supersededCandidate.UpdatedUtc = supersededCandidate.DecisionUtc.Value;
            supersededCandidate.ArchiveReason = null;
            supersededCandidate.ArchivedUtc = null;
            supersededCandidate.Revision = candidate.Revision + 1;

            if (await discoveryRunCandidateStore.TryUpdateAsync(supersededCandidate, candidate.Revision, cancellationToken))
            {
                await UpsertDispositionAsync(run, supersededCandidate, DiscoveryRunCandidateStates.Superseded, acceptedCandidate.CandidateKey, cancellationToken);
            }
        }
    }

    private async Task UpsertDispositionAsync(DiscoveryRun run, DiscoveryRunCandidate candidate, string state, string? supersededByCandidateKey, CancellationToken cancellationToken)
    {
        var disposition = new DiscoveryRunCandidateDisposition
        {
            Id = BuildDispositionId(run, candidate, state),
            State = state,
            ScopeFingerprint = DiscoveryRunScopePolicy.CreateFingerprint(run),
            RequestedCategoryKeys = run.RequestedCategoryKeys.ToArray(),
            Market = run.Market,
            Locale = run.Locale,
            NormalizedHost = DiscoveryRunCandidateIdentity.GetNormalizedHost(candidate),
            NormalizedBaseUrl = DiscoveryRunCandidateIdentity.GetNormalizedBaseUrl(candidate),
            NormalizedDisplayName = DiscoveryRunCandidateIdentity.GetNormalizedDisplayName(candidate),
            AllowedMarkets = DiscoveryRunCandidateIdentity.NormalizeMarkets(candidate.AllowedMarkets),
            SourceRunId = run.RunId,
            SourceCandidateKey = candidate.CandidateKey,
            SupersededByCandidateKey = supersededByCandidateKey,
            IsActive = true,
            CreatedUtc = candidate.DecisionUtc ?? candidate.UpdatedUtc,
            UpdatedUtc = candidate.DecisionUtc ?? candidate.UpdatedUtc,
            RestoredUtc = null
        };

        await discoveryRunCandidateDispositionStore.UpsertAsync(disposition, cancellationToken);
    }

    private async Task DeactivateMatchingDispositionsAsync(DiscoveryRun run, DiscoveryRunCandidate candidate, CancellationToken cancellationToken)
    {
        var matches = await discoveryRunCandidateDispositionStore.FindActiveMatchesAsync(
            DiscoveryRunScopePolicy.CreateFingerprint(run),
            DiscoveryRunCandidateIdentity.GetNormalizedHost(candidate),
            DiscoveryRunCandidateIdentity.GetNormalizedBaseUrl(candidate),
            DiscoveryRunCandidateIdentity.GetNormalizedDisplayName(candidate),
            candidate.AllowedMarkets,
            cancellationToken);

        var utcNow = DateTime.UtcNow;
        foreach (var disposition in matches)
        {
            disposition.IsActive = false;
            disposition.RestoredUtc = utcNow;
            disposition.UpdatedUtc = utcNow;
            await discoveryRunCandidateDispositionStore.UpsertAsync(disposition, cancellationToken);
        }
    }

    private static string DetermineRestoreState(DiscoveryRunCandidate candidate)
    {
        if (!string.IsNullOrWhiteSpace(candidate.SuppressionDispositionId)
            || string.Equals(candidate.PreviousState, DiscoveryRunCandidateStates.Dismissed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.PreviousState, DiscoveryRunCandidateStates.Superseded, StringComparison.OrdinalIgnoreCase))
        {
            return DiscoveryRunCandidateStates.Suggested;
        }

        return string.IsNullOrWhiteSpace(candidate.PreviousState)
            ? DiscoveryRunCandidateStates.Suggested
            : candidate.PreviousState;
    }

    private static string BuildDispositionId(DiscoveryRun run, DiscoveryRunCandidate candidate, string state)
    {
        return string.Join(
            "|",
            DiscoveryRunScopePolicy.CreateFingerprint(run),
            state,
            DiscoveryRunCandidateIdentity.GetNormalizedHost(candidate),
            DiscoveryRunCandidateIdentity.GetNormalizedBaseUrl(candidate),
            DiscoveryRunCandidateIdentity.GetNormalizedDisplayName(candidate));
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

    private static int GetNextRevision(int currentRevision, int expectedRevision)
    {
        if (expectedRevision <= 0)
        {
            throw new InvalidOperationException("Candidate revision must be provided for optimistic concurrency.");
        }

        return Math.Max(currentRevision, expectedRevision) + 1;
    }

    private static InvalidOperationException CreateConcurrencyException(string candidateKey)
    {
        return new InvalidOperationException($"Candidate '{candidateKey}' changed while this action was in progress. Refresh the run and retry.");
    }

    private static bool IsDuplicateSourceRegistrationException(ArgumentException exception, string sourceId)
    {
        return string.Equals(exception.ParamName, "registration", StringComparison.Ordinal)
            && exception.Message.Contains($"Source '{sourceId}' already exists.", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<DiscoveryRun> RequireRunAsync(string runId, CancellationToken cancellationToken)
    {
        return await discoveryRunStore.GetAsync(NormalizeRequired(runId, nameof(runId)), cancellationToken)
            ?? throw new KeyNotFoundException($"Discovery run '{runId}' was not found.");
    }

    private static string NormalizeRequired(string value, string paramName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A value is required.", paramName)
            : value.Trim();
    }

    private static string NormalizeCandidateStateFilter(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? DiscoveryRunCandidateStateFilters.All
            : value.Trim().ToLowerInvariant();

        return normalized switch
        {
            DiscoveryRunCandidateStateFilters.All => DiscoveryRunCandidateStateFilters.All,
            DiscoveryRunCandidateStateFilters.Active => DiscoveryRunCandidateStateFilters.Active,
            DiscoveryRunCandidateStateFilters.Archived => DiscoveryRunCandidateStateFilters.Archived,
            _ => normalized
        };
    }

    private static string NormalizeCandidateSort(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? DiscoveryRunCandidateSortModes.ReviewPriority
            : value.Trim().ToLowerInvariant() switch
            {
                DiscoveryRunCandidateSortModes.ReviewPriority => DiscoveryRunCandidateSortModes.ReviewPriority,
                DiscoveryRunCandidateSortModes.ConfidenceDesc => DiscoveryRunCandidateSortModes.ConfidenceDesc,
                DiscoveryRunCandidateSortModes.DuplicateRiskAsc => DiscoveryRunCandidateSortModes.DuplicateRiskAsc,
                DiscoveryRunCandidateSortModes.UpdatedDesc => DiscoveryRunCandidateSortModes.UpdatedDesc,
                _ => DiscoveryRunCandidateSortModes.ReviewPriority
            };
    }

    private DiscoveryRun BuildQueuedRun(
        IReadOnlyList<string> categoryKeys,
        string? locale,
        string? market,
        string automationMode,
        IReadOnlyList<string> brandHints,
        int maxCandidates,
        string triggerKind,
        string? recurringCampaignId,
        string? recurringCampaignFingerprint,
        string statusMessage)
    {
        var llmStatus = llmStatusProvider?.GetStatus() ?? new LlmServiceStatus
        {
            Code = LlmStatusCodes.Disabled,
            Message = "LLM validation status is not exposed by the current classifier."
        };

        var utcNow = DateTime.UtcNow;
        return new DiscoveryRun
        {
            RunId = $"discovery_run_{Guid.NewGuid():N}",
            TriggerKind = triggerKind,
            RecurringCampaignId = recurringCampaignId,
            RecurringCampaignFingerprint = recurringCampaignFingerprint,
            RequestedCategoryKeys = categoryKeys,
            Locale = locale,
            Market = market,
            AutomationMode = automationMode,
            BrandHints = brandHints,
            MaxCandidates = maxCandidates,
            Status = DiscoveryRunStatuses.Queued,
            CurrentStage = DiscoveryRunStageNames.Search,
            StatusMessage = statusMessage,
            LlmStatus = llmStatus.Code,
            LlmStatusMessage = llmStatus.Message,
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow
        };
    }
}