using ProductNormaliser.Application.AI;
using ProductNormaliser.Application.Categories;
using ProductNormaliser.Application.Governance;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

public sealed class DiscoveryRunService(
    IDiscoveryRunStore discoveryRunStore,
    IDiscoveryRunCandidateStore discoveryRunCandidateStore,
    ICategoryMetadataService categoryMetadataService,
    ISourceManagementService sourceManagementService,
    IManagementAuditService managementAuditService,
    ILlmStatusProvider? llmStatusProvider = null) : IDiscoveryRunService
{
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

        var llmStatus = llmStatusProvider?.GetStatus() ?? new LlmServiceStatus
        {
            Code = LlmStatusCodes.Disabled,
            Message = "LLM validation status is not exposed by the current classifier."
        };

        var utcNow = DateTime.UtcNow;
        var run = new DiscoveryRun
        {
            RunId = $"discovery_run_{Guid.NewGuid():N}",
            RequestedCategoryKeys = categoryKeys,
            Locale = SourceCandidateDiscoveryEvaluator.NormalizeOptionalText(request.Locale),
            Market = SourceCandidateDiscoveryEvaluator.NormalizeOptionalText(request.Market),
            AutomationMode = SourceAutomationModes.Normalize(request.AutomationMode),
            BrandHints = SourceCandidateDiscoveryEvaluator.NormalizeValues(request.BrandHints),
            MaxCandidates = SourceCandidateDiscoveryEvaluator.NormalizeMaxCandidates(request.MaxCandidates),
            Status = DiscoveryRunStatuses.Queued,
            CurrentStage = DiscoveryRunStageNames.Search,
            StatusMessage = "Discovery run is queued and waiting for worker capacity.",
            LlmStatus = llmStatus.Code,
            LlmStatusMessage = llmStatus.Message,
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow
        };

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

    public Task<DiscoveryRun?> GetAsync(string runId, CancellationToken cancellationToken = default)
    {
        return discoveryRunStore.GetAsync(NormalizeRequired(runId, nameof(runId)), cancellationToken);
    }

    public async Task<IReadOnlyList<DiscoveryRunCandidate>> ListCandidatesAsync(string runId, CancellationToken cancellationToken = default)
    {
        return await discoveryRunCandidateStore.ListByRunAsync(NormalizeRequired(runId, nameof(runId)), cancellationToken);
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

    public async Task<DiscoveryRunCandidate?> AcceptCandidateAsync(string runId, string candidateKey, CancellationToken cancellationToken = default)
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

        var registration = new CrawlSourceRegistration
        {
            SourceId = candidate.CandidateKey,
            DisplayName = candidate.DisplayName,
            BaseUrl = candidate.BaseUrl,
            AllowedMarkets = candidate.AllowedMarkets.Count == 0 && !string.IsNullOrWhiteSpace(run.Market)
                ? [run.Market]
                : candidate.AllowedMarkets,
            PreferredLocale = candidate.PreferredLocale ?? run.Locale,
            AutomationPolicy = new SourceAutomationPolicy { Mode = run.AutomationMode },
            SupportedCategoryKeys = candidate.MatchedCategoryKeys.Count == 0 ? run.RequestedCategoryKeys : candidate.MatchedCategoryKeys,
            IsEnabled = true
        };

        var source = await sourceManagementService.RegisterAsync(registration, cancellationToken);
        candidate.AcceptedSourceId = source.Id;
        candidate.PreviousState = candidate.State;
        candidate.State = DiscoveryRunCandidateStates.ManuallyAccepted;
        candidate.StateMessage = $"Accepted and registered as source '{source.Id}'.";
        candidate.DecisionUtc = DateTime.UtcNow;
        candidate.UpdatedUtc = candidate.DecisionUtc.Value;
        await discoveryRunCandidateStore.UpsertAsync(candidate, cancellationToken);
        return candidate;
    }

    public async Task<DiscoveryRunCandidate?> DismissCandidateAsync(string runId, string candidateKey, CancellationToken cancellationToken = default)
    {
        _ = await RequireRunAsync(runId, cancellationToken);
        var candidate = await discoveryRunCandidateStore.GetAsync(NormalizeRequired(runId, nameof(runId)), NormalizeRequired(candidateKey, nameof(candidateKey)), cancellationToken);
        if (candidate is null)
        {
            return null;
        }

        if (!DiscoveryRunStateMachine.CanDismissCandidate(candidate.State))
        {
            throw new InvalidOperationException($"Candidate '{candidate.CandidateKey}' cannot be dismissed from state '{candidate.State}'.");
        }

        candidate.PreviousState = candidate.State;
        candidate.State = DiscoveryRunCandidateStates.Dismissed;
        candidate.StateMessage = "Dismissed by operator.";
        candidate.DecisionUtc = DateTime.UtcNow;
        candidate.UpdatedUtc = candidate.DecisionUtc.Value;
        await discoveryRunCandidateStore.UpsertAsync(candidate, cancellationToken);
        return candidate;
    }

    public async Task<DiscoveryRunCandidate?> RestoreCandidateAsync(string runId, string candidateKey, CancellationToken cancellationToken = default)
    {
        _ = await RequireRunAsync(runId, cancellationToken);
        var candidate = await discoveryRunCandidateStore.GetAsync(NormalizeRequired(runId, nameof(runId)), NormalizeRequired(candidateKey, nameof(candidateKey)), cancellationToken);
        if (candidate is null)
        {
            return null;
        }

        if (!DiscoveryRunStateMachine.CanRestoreCandidate(candidate.State))
        {
            throw new InvalidOperationException($"Candidate '{candidate.CandidateKey}' cannot be restored from state '{candidate.State}'.");
        }

        candidate.State = string.IsNullOrWhiteSpace(candidate.PreviousState)
            ? DiscoveryRunCandidateStates.Suggested
            : candidate.PreviousState;
        candidate.StateMessage = "Restored to the active candidate queue.";
        candidate.UpdatedUtc = DateTime.UtcNow;
        await discoveryRunCandidateStore.UpsertAsync(candidate, cancellationToken);
        return candidate;
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
}