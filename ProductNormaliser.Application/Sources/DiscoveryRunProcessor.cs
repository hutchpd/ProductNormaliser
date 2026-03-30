using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProductNormaliser.Application.Governance;
using ProductNormaliser.Application.Observability;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

public sealed class DiscoveryRunProcessor(
    IDiscoveryRunStore discoveryRunStore,
    IDiscoveryRunCandidateStore discoveryRunCandidateStore,
    IDiscoveryRunCandidateDispositionStore discoveryRunCandidateDispositionStore,
    ICrawlSourceStore crawlSourceStore,
    ISourceManagementService sourceManagementService,
    ICrawlGovernanceService crawlGovernanceService,
    ISourceCandidateSearchProvider sourceCandidateSearchProvider,
    ISourceCandidateProbeService sourceCandidateProbeService,
    IOptions<SourceOnboardingAutomationOptions> onboardingAutomationOptions,
    IOptions<DiscoveryRunOperationsOptions> operationsOptions,
    ILogger<DiscoveryRunProcessor>? logger = null) : IDiscoveryRunProcessor
{
    private readonly SourceCandidateDiscoveryEvaluator evaluator = new(onboardingAutomationOptions.Value);
    private readonly SourceOnboardingAutomationOptions onboardingOptions = onboardingAutomationOptions.Value;
    private readonly DiscoveryRunOperationsOptions operationsOptions = operationsOptions.Value;
    private readonly ILogger<DiscoveryRunProcessor> logger = logger ?? NullLogger<DiscoveryRunProcessor>.Instance;

    public DiscoveryRunProcessor(
        IDiscoveryRunStore discoveryRunStore,
        IDiscoveryRunCandidateStore discoveryRunCandidateStore,
        ICrawlSourceStore crawlSourceStore,
        ISourceManagementService sourceManagementService,
        ICrawlGovernanceService crawlGovernanceService,
        ISourceCandidateSearchProvider sourceCandidateSearchProvider,
        ISourceCandidateProbeService sourceCandidateProbeService,
        IOptions<SourceOnboardingAutomationOptions> onboardingAutomationOptions,
        ILogger<DiscoveryRunProcessor>? logger = null)
        : this(
            discoveryRunStore,
            discoveryRunCandidateStore,
            NullDiscoveryRunCandidateDispositionStore.Instance,
            crawlSourceStore,
            sourceManagementService,
            crawlGovernanceService,
            sourceCandidateSearchProvider,
            sourceCandidateProbeService,
            onboardingAutomationOptions,
            Options.Create(new DiscoveryRunOperationsOptions()),
            logger)
    {
    }

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        var run = await discoveryRunStore.GetNextQueuedAsync(cancellationToken);
        if (run is null)
        {
            return false;
        }

        logger.LogInformation(
            "Claimed discovery run {RunId} for categories [{Categories}] market={Market} locale={Locale} automation={AutomationMode} maxCandidates={MaxCandidates}",
            run.RunId,
            string.Join(", ", run.RequestedCategoryKeys),
            run.Market ?? "any",
            run.Locale ?? "any",
            run.AutomationMode,
            run.MaxCandidates);

        var utcNow = DateTime.UtcNow;
        var scopeFingerprint = DiscoveryRunScopePolicy.CreateFingerprint(run);
        run.Status = DiscoveryRunStatuses.Running;
        run.StartedUtc ??= utcNow;
        run.UpdatedUtc = utcNow;
        run.LastHeartbeatUtc = utcNow;
        run.StatusMessage = "Worker picked up the discovery run and is preparing search queries.";
        run.SearchTimeoutBudgetMs = Math.Max(1, operationsOptions.SearchTimeoutSeconds) * 1000L;
        run.ProbeTimeoutBudgetMs = Math.Max(1, operationsOptions.ProbeTimeoutSeconds) * 1000L;
        run.LlmTimeoutBudgetMs = Math.Max(1, operationsOptions.LlmVerificationTimeoutMs);
        EnsureBudgetDiagnostics(run);
        ProductNormaliserTelemetry.DiscoveryRunsStarted.Add(1, new TagList { { "automation_mode", run.AutomationMode } });
        await discoveryRunStore.UpsertAsync(run, cancellationToken);

        try
        {
            using var activity = ProductNormaliserTelemetry.ActivitySource.StartActivity("discovery.run.process", ActivityKind.Internal);
            activity?.SetTag("discovery.run_id", run.RunId);
            activity?.SetTag("discovery.automation_mode", run.AutomationMode);

            var request = new DiscoverSourceCandidatesRequest
            {
                CategoryKeys = run.RequestedCategoryKeys,
                Locale = run.Locale,
                Market = run.Market,
                AutomationMode = run.AutomationMode,
                BrandHints = run.BrandHints,
                MaxCandidates = run.MaxCandidates
            };

            await UpdateRunAsync(run, DiscoveryRunStageNames.Search, "Searching likely hosts for the requested category and market scope.", cancellationToken);
            SourceCandidateSearchResponse searchResponse;
            var searchStopwatch = Stopwatch.StartNew();
            try
            {
                searchResponse = await sourceCandidateSearchProvider.SearchAsync(request, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                searchResponse = new SourceCandidateSearchResponse
                {
                    Diagnostics =
                    [
                        new SourceCandidateDiscoveryDiagnostic
                        {
                            Code = "search_timeout",
                            Severity = SourceCandidateDiscoveryDiagnostic.SeverityWarning,
                            Title = "Search provider timed out",
                            Message = $"Search exceeded the configured provider budget of {run.SearchTimeoutBudgetMs}ms. The run continued with zero search results."
                        }
                    ]
                };
            }

            run.SearchElapsedMs = searchStopwatch.ElapsedMilliseconds;
            ProductNormaliserTelemetry.DiscoverySearchDurationMs.Record(run.SearchElapsedMs.Value, new TagList { { "automation_mode", run.AutomationMode } });
            run.SearchResultCount = searchResponse.Candidates.Count;
            run.Diagnostics = searchResponse.Diagnostics.Select(MapDiagnostic).ToList();
            EnsureBudgetDiagnostics(run);
            await TouchRunAsync(run, cancellationToken);

            logger.LogInformation(
                "Discovery run {RunId} search finished with {SearchResultCount} raw results and {DiagnosticCount} diagnostic(s) in {ElapsedMs}ms",
                run.RunId,
                run.SearchResultCount,
                run.Diagnostics.Count,
                run.SearchElapsedMs);

            if (await TryRespectOperatorControlsAsync(run.RunId, cancellationToken))
            {
                return true;
            }

            await UpdateRunAsync(run, DiscoveryRunStageNames.CollapseAndDedupe, "Collapsing equivalent hosts and removing duplicate search hits.", cancellationToken);
            var collapsedCandidates = evaluator.CollapseEquivalentCandidates(searchResponse.Candidates);
            run.CollapsedCandidateCount = collapsedCandidates.Count;
            await TouchRunAsync(run, cancellationToken);

            logger.LogInformation(
                "Discovery run {RunId} collapsed search results into {CollapsedCandidateCount} candidate slot(s)",
                run.RunId,
                run.CollapsedCandidateCount);

            foreach (var searchCandidate in collapsedCandidates)
            {
                var now = DateTime.UtcNow;
                var pendingCandidate = new DiscoveryRunCandidate
                {
                    Id = $"{run.RunId}:{searchCandidate.CandidateKey}",
                    RunId = run.RunId,
                    CandidateKey = string.IsNullOrWhiteSpace(searchCandidate.CandidateKey) ? searchCandidate.Host : searchCandidate.CandidateKey,
                    Revision = 1,
                    State = DiscoveryRunCandidateStates.Pending,
                    DisplayName = searchCandidate.DisplayName,
                    BaseUrl = searchCandidate.BaseUrl,
                    Host = searchCandidate.Host,
                    CandidateType = searchCandidate.CandidateType,
                    AllowedMarkets = searchCandidate.AllowedMarkets,
                    PreferredLocale = searchCandidate.PreferredLocale,
                    MarketEvidence = searchCandidate.MarketEvidence,
                    LocaleEvidence = searchCandidate.LocaleEvidence,
                    MatchedCategoryKeys = searchCandidate.MatchedCategoryKeys,
                    MatchedBrandHints = searchCandidate.MatchedBrandHints,
                    CreatedUtc = now,
                    UpdatedUtc = now,
                    StateMessage = "Waiting to be probed by the background worker."
                };

                var suppression = await FindActiveDispositionAsync(scopeFingerprint, searchCandidate, cancellationToken);
                if (suppression is not null)
                {
                    pendingCandidate.PreviousState = suppression.State;
                    pendingCandidate.State = DiscoveryRunCandidateStates.Archived;
                    pendingCandidate.SuppressionDispositionId = suppression.Id;
                    pendingCandidate.ArchiveReason = string.Equals(suppression.State, DiscoveryRunCandidateStates.Dismissed, StringComparison.OrdinalIgnoreCase)
                        ? "historical_dismissal"
                        : "historical_supersession";
                    pendingCandidate.ArchivedUtc = now;
                    pendingCandidate.StateMessage = CreateHistoricalSuppressionMessage(suppression);
                    ProductNormaliserTelemetry.DiscoveryCandidatesArchived.Add(1, new TagList { { "reason", pendingCandidate.ArchiveReason } });
                }

                await discoveryRunCandidateStore.UpsertAsync(pendingCandidate, cancellationToken);
            }

            var registeredSources = await crawlSourceStore.ListAsync(cancellationToken);
            var evaluatedCandidates = new List<SourceCandidateResult>(collapsedCandidates.Count);
            for (var index = 0; index < collapsedCandidates.Count; index++)
            {
                if (await TryRespectOperatorControlsAsync(run.RunId, cancellationToken))
                {
                    return true;
                }

                var searchCandidate = collapsedCandidates[index];
                var candidateKey = string.IsNullOrWhiteSpace(searchCandidate.CandidateKey) ? searchCandidate.Host : searchCandidate.CandidateKey;
                var storedCandidate = await discoveryRunCandidateStore.GetAsync(run.RunId, candidateKey, cancellationToken);
                if (storedCandidate is null || !DiscoveryRunStateMachine.CanWorkerProgressCandidate(storedCandidate.State))
                {
                    continue;
                }

                var candidateInProgress = CloneCandidate(storedCandidate);
                if (string.Equals(run.LlmStatus, "active", StringComparison.OrdinalIgnoreCase))
                {
                    await UpdateRunAsync(run, DiscoveryRunStageNames.LlmVerify, $"Running serial local verification for candidate {index + 1} of {collapsedCandidates.Count}.", cancellationToken);
                    candidateInProgress.State = DiscoveryRunCandidateStates.AwaitingLlm;
                    candidateInProgress.StateMessage = "Queued for serial local LLM verification. This stage intentionally runs with low concurrency.";
                }
                else
                {
                    await UpdateRunAsync(run, DiscoveryRunStageNames.Probe, $"Probing representative pages for candidate {index + 1} of {collapsedCandidates.Count}.", cancellationToken);
                    candidateInProgress.State = DiscoveryRunCandidateStates.Probing;
                    candidateInProgress.StateMessage = "Probing representative category and product pages.";
                }

                candidateInProgress.UpdatedUtc = DateTime.UtcNow;
                candidateInProgress.Revision = storedCandidate.Revision + 1;
                if (!await discoveryRunCandidateStore.TryUpdateAsync(candidateInProgress, storedCandidate.Revision, cancellationToken))
                {
                    continue;
                }

                SourceCandidateProbeResult probe;
                try
                {
                    probe = await sourceCandidateProbeService.ProbeAsync(searchCandidate, run.RequestedCategoryKeys, run.AutomationMode, cancellationToken);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    probe = new SourceCandidateProbeResult();
                    AppendUniqueDiagnostic(run.Diagnostics, new DiscoveryRunDiagnostic
                    {
                        Code = "probe_timeout",
                        Severity = SourceCandidateDiscoveryDiagnostic.SeverityWarning,
                        Title = "Candidate probing timed out",
                        Message = $"Probing exceeded the configured probe budget of {run.ProbeTimeoutBudgetMs}ms for '{searchCandidate.DisplayName}'. The worker continued with reduced confidence for this candidate."
                    });
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Candidate probing failed for discovery run {RunId} candidate {CandidateKey}", run.RunId, candidateKey);
                    probe = new SourceCandidateProbeResult();
                    AppendUniqueDiagnostic(run.Diagnostics, new DiscoveryRunDiagnostic
                    {
                        Code = "probe_failed",
                        Severity = SourceCandidateDiscoveryDiagnostic.SeverityWarning,
                        Title = "Candidate probing failed",
                        Message = $"Probing failed for '{searchCandidate.DisplayName}'. The worker continued with reduced confidence for this candidate."
                    });
                }

                run.ProbeTotalElapsedMs += probe.ProbeElapsedMs;
                ProductNormaliserTelemetry.DiscoveryProbeDurationMs.Record(probe.ProbeElapsedMs, new TagList { { "automation_mode", run.AutomationMode } });

                var duplicateSources = registeredSources
                    .Where(source => evaluator.IsPotentialDuplicate(source, searchCandidate))
                    .ToArray();

                var governanceWarning = default(string);
                var allowedByGovernance = true;
                try
                {
                    crawlGovernanceService.ValidateSourceBaseUrl(searchCandidate.BaseUrl, nameof(searchCandidate.BaseUrl));
                }
                catch (ArgumentException exception)
                {
                    allowedByGovernance = false;
                    governanceWarning = exception.Message;
                }

                var candidateResult = evaluator.BuildCandidateResult(searchCandidate, request, probe, duplicateSources, governanceWarning, allowedByGovernance);
                evaluatedCandidates.Add(candidateResult);

                var currentCandidate = await discoveryRunCandidateStore.GetAsync(run.RunId, candidateKey, cancellationToken);
                if (currentCandidate is null || !DiscoveryRunStateMachine.CanWorkerProgressCandidate(currentCandidate.State))
                {
                    continue;
                }

                var proposedState = DetermineCandidateState(candidateResult, run, onboardingOptions);
                var mappedCandidate = DiscoveryRunMapper.ToDocument(run.RunId, candidateResult, proposedState, currentCandidate.CreatedUtc);
                mappedCandidate.Revision = currentCandidate.Revision + 1;
                mappedCandidate.CreatedUtc = currentCandidate.CreatedUtc;
                mappedCandidate.PreviousState = currentCandidate.PreviousState;
                mappedCandidate.SupersededByCandidateKey = currentCandidate.SupersededByCandidateKey;
                mappedCandidate.SuppressionDispositionId = currentCandidate.SuppressionDispositionId;
                mappedCandidate.ArchiveReason = currentCandidate.ArchiveReason;
                mappedCandidate.ArchivedUtc = currentCandidate.ArchivedUtc;
                mappedCandidate.StateMessage = mappedCandidate.State switch
                {
                    DiscoveryRunCandidateStates.AutoAccepted => "Guardrails passed. Publishing source registration immediately.",
                    DiscoveryRunCandidateStates.Suggested when candidateResult.AutomationAssessment.EligibleForAutoAccept => "Guardrails passed, but the auto-accept cap is already consumed for this run. Ready for operator review.",
                    DiscoveryRunCandidateStates.Suggested => "Ready for operator review.",
                    _ => "Candidate did not clear guarded acceptance thresholds."
                };
                mappedCandidate.UpdatedUtc = DateTime.UtcNow;
                if (!await discoveryRunCandidateStore.TryUpdateAsync(mappedCandidate, currentCandidate.Revision, cancellationToken))
                {
                    continue;
                }

                if (string.Equals(mappedCandidate.State, DiscoveryRunCandidateStates.AutoAccepted, StringComparison.OrdinalIgnoreCase))
                {
                    run.AutoAcceptedCandidateCount += 1;
                    var publishedCandidate = await PublishAcceptedCandidateAsync(run, mappedCandidate, autoAccepted: true, cancellationToken);
                    if (publishedCandidate is not null)
                    {
                        RegisterAcceptedCandidate(run, publishedCandidate);
                        registeredSources = registeredSources.Append(new CrawlSource
                        {
                            Id = publishedCandidate.AcceptedSourceId ?? publishedCandidate.CandidateKey,
                            DisplayName = publishedCandidate.DisplayName,
                            BaseUrl = publishedCandidate.BaseUrl,
                            Host = publishedCandidate.Host,
                            AllowedMarkets = publishedCandidate.AllowedMarkets.ToList(),
                            PreferredLocale = publishedCandidate.PreferredLocale ?? run.Locale ?? "en-GB",
                            SupportedCategoryKeys = publishedCandidate.MatchedCategoryKeys.ToList(),
                            AutomationPolicy = new SourceAutomationPolicy { Mode = run.AutomationMode },
                            ThrottlingPolicy = new SourceThrottlingPolicy(),
                            IsEnabled = true,
                            CreatedUtc = DateTime.UtcNow,
                            UpdatedUtc = DateTime.UtcNow
                        }).ToArray();
                    }
                }

                run.ProbeCompletedCount += 1;
                run.ProbeAverageElapsedMs = (long)Math.Round(run.ProbeTotalElapsedMs / (double)Math.Max(1, run.ProbeCompletedCount), MidpointRounding.AwayFromZero);
                if (probe.LlmElapsedMs is > 0)
                {
                    run.LlmCompletedCount += 1;
                    run.LlmTotalElapsedMs += probe.LlmElapsedMs.Value;
                    run.LlmAverageElapsedMs = (long)Math.Round(run.LlmTotalElapsedMs / (double)run.LlmCompletedCount, MidpointRounding.AwayFromZero);
                    ProductNormaliserTelemetry.DiscoveryLlmDurationMs.Record(probe.LlmElapsedMs.Value, new TagList { { "automation_mode", run.AutomationMode } });
                }

                run.LlmQueueDepth = Math.Max(0, collapsedCandidates.Count - (index + 1));
                ProductNormaliserTelemetry.DiscoveryLlmQueueDepth.Record(run.LlmQueueDepth, new TagList { { "automation_mode", run.AutomationMode } });
                await RefreshRunSummaryAsync(run, cancellationToken);
                await TouchRunAsync(run, cancellationToken);
                ProductNormaliserTelemetry.DiscoveryCandidatesProcessed.Add(1, new TagList
                {
                    { "state", mappedCandidate.State },
                    { "automation_mode", run.AutomationMode }
                });
            }

            await UpdateRunAsync(run, DiscoveryRunStageNames.Score, "Scoring candidate confidence and weighting extraction signals.", cancellationToken);
            AppendDiagnostics(run.Diagnostics, evaluator.BuildProbeDiagnostics(evaluatedCandidates));
            AppendDiagnostics(run.Diagnostics, evaluator.BuildLlmDiagnostics(evaluatedCandidates));
            await TouchRunAsync(run, cancellationToken);

            await UpdateRunAsync(run, DiscoveryRunStageNames.Decide, "Assigning guarded decisions for suggested and auto-accepted candidates.", cancellationToken);
            var currentCandidates = await RefreshRunSummaryAsync(run, cancellationToken);
            await TouchRunAsync(run, cancellationToken);

            await UpdateRunAsync(run, DiscoveryRunStageNames.Publish, "Publishing auto-accepted candidates into the source registry.", cancellationToken);
            foreach (var candidate in currentCandidates
                .Where(candidate => string.Equals(candidate.State, DiscoveryRunCandidateStates.AutoAccepted, StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrWhiteSpace(candidate.AcceptedSourceId))
                .ToArray())
            {
                if (await TryRespectOperatorControlsAsync(run.RunId, cancellationToken))
                {
                    return true;
                }

                var publishedCandidate = await PublishAcceptedCandidateAsync(run, candidate, autoAccepted: true, cancellationToken);
                if (publishedCandidate is not null)
                {
                    RegisterAcceptedCandidate(run, publishedCandidate);
                }
            }

            await RefreshRunSummaryAsync(run, cancellationToken);
            run.Status = DiscoveryRunStatuses.Completed;
            run.CompletedUtc = DateTime.UtcNow;
            run.StatusMessage = "Discovery run completed. Suggested and auto-accepted candidates are ready for review.";
            run.UpdatedUtc = run.CompletedUtc.Value;
            run.LastHeartbeatUtc = run.CompletedUtc.Value;
            await discoveryRunStore.UpsertAsync(run, cancellationToken);
            EmitRunTelemetry(run);

            logger.LogInformation(
                "Completed discovery run {RunId} with status={Status}, suggested={SuggestedCandidateCount}, autoAccepted={AutoAcceptedCandidateCount}, published={PublishedCandidateCount}",
                run.RunId,
                run.Status,
                run.SuggestedCandidateCount,
                run.AutoAcceptedCandidateCount,
                run.PublishedCandidateCount);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled discovery run failure for {RunId}", run.RunId);
            run.Status = DiscoveryRunStatuses.Failed;
            run.FailureMessage = exception.Message;
            run.StatusMessage = "Discovery run failed before candidate publication completed.";
            run.CompletedUtc = DateTime.UtcNow;
            run.UpdatedUtc = run.CompletedUtc.Value;
            run.LastHeartbeatUtc = run.CompletedUtc.Value;
            AppendUniqueDiagnostic(run.Diagnostics, new DiscoveryRunDiagnostic
            {
                Code = "run_failed",
                Severity = SourceCandidateDiscoveryDiagnostic.SeverityError,
                Title = "Discovery run failed",
                Message = exception.Message
            });
            await discoveryRunStore.UpsertAsync(run, cancellationToken);
            EmitRunTelemetry(run);
        }

        return true;
    }

    private async Task<bool> TryRespectOperatorControlsAsync(string runId, CancellationToken cancellationToken)
    {
        var current = await discoveryRunStore.GetAsync(runId, cancellationToken);
        if (current is null)
        {
            return true;
        }

        if (string.Equals(current.Status, DiscoveryRunStatuses.Paused, StringComparison.OrdinalIgnoreCase))
        {
            current.StatusMessage = "Discovery run is paused. Resume to continue background execution.";
            current.UpdatedUtc = DateTime.UtcNow;
            current.LastHeartbeatUtc = current.UpdatedUtc;
            await discoveryRunStore.UpsertAsync(current, cancellationToken);
            return true;
        }

        if (string.Equals(current.Status, DiscoveryRunStatuses.CancelRequested, StringComparison.OrdinalIgnoreCase))
        {
            current.Status = DiscoveryRunStatuses.Cancelled;
            current.CompletedUtc = DateTime.UtcNow;
            current.StatusMessage = "Discovery run was cancelled after the current unit of work completed.";
            current.UpdatedUtc = current.CompletedUtc.Value;
            current.LastHeartbeatUtc = current.CompletedUtc.Value;
            await discoveryRunStore.UpsertAsync(current, cancellationToken);
            return true;
        }

        return false;
    }

    private async Task UpdateRunAsync(DiscoveryRun run, string stage, string message, CancellationToken cancellationToken)
    {
        run.CurrentStage = stage;
        run.StatusMessage = message;
        await TouchRunAsync(run, cancellationToken);
    }

    private async Task TouchRunAsync(DiscoveryRun run, CancellationToken cancellationToken)
    {
        run.UpdatedUtc = DateTime.UtcNow;
        run.LastHeartbeatUtc = run.UpdatedUtc;
        await discoveryRunStore.UpsertAsync(run, cancellationToken);
    }

    private async Task<DiscoveryRunCandidate?> PublishAcceptedCandidateAsync(DiscoveryRun run, DiscoveryRunCandidate candidate, bool autoAccepted, CancellationToken cancellationToken)
    {
        try
        {
            var source = await sourceManagementService.RegisterAsync(new CrawlSourceRegistration
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
            }, cancellationToken);

            candidate.AcceptedSourceId = source.Id;
            candidate.StateMessage = autoAccepted
                ? $"Auto-accepted and registered as source '{source.Id}'."
                : $"Accepted and registered as source '{source.Id}'.";
            candidate.DecisionUtc = DateTime.UtcNow;
            candidate.UpdatedUtc = candidate.DecisionUtc.Value;
            candidate.Revision += 1;
            await discoveryRunCandidateStore.UpsertAsync(candidate, cancellationToken);
            await SupersedeDuplicateCandidatesAsync(run, candidate, cancellationToken);
            return candidate;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Publish failed for discovery run {RunId} candidate {CandidateKey}", run.RunId, candidate.CandidateKey);
            candidate.PreviousState = candidate.State;
            candidate.State = DiscoveryRunCandidateStates.Failed;
            candidate.StateMessage = $"{(autoAccepted ? "Auto-accept" : "Acceptance")} publish failed: {exception.Message}";
            candidate.DecisionUtc = DateTime.UtcNow;
            candidate.UpdatedUtc = candidate.DecisionUtc.Value;
            candidate.Revision += 1;
            await discoveryRunCandidateStore.UpsertAsync(candidate, cancellationToken);
            return null;
        }
    }

    private async Task<IReadOnlyList<DiscoveryRunCandidate>> RefreshRunSummaryAsync(DiscoveryRun run, CancellationToken cancellationToken)
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
        var acceptedCandidates = candidates.Count(candidate => !string.IsNullOrWhiteSpace(candidate.AcceptedSourceId)
            || string.Equals(candidate.State, DiscoveryRunCandidateStates.AutoAccepted, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.State, DiscoveryRunCandidateStates.ManuallyAccepted, StringComparison.OrdinalIgnoreCase));
        var manualReviewCandidates = candidates.Count(candidate => string.Equals(candidate.State, DiscoveryRunCandidateStates.Suggested, StringComparison.OrdinalIgnoreCase));
        if (processedCandidates > 0)
        {
            run.AcceptanceRate = decimal.Round(acceptedCandidates / (decimal)processedCandidates, 4, MidpointRounding.AwayFromZero);
            run.ManualReviewRate = decimal.Round(manualReviewCandidates / (decimal)processedCandidates, 4, MidpointRounding.AwayFromZero);
        }

        if (run.StartedUtc is not null)
        {
            var elapsedMinutes = Math.Max(1d / 60d, Math.Max(0d, (DateTime.UtcNow - run.StartedUtc.Value).TotalMinutes));
            run.CandidateThroughputPerMinute = decimal.Round((decimal)(run.ProbeCompletedCount / elapsedMinutes), 4, MidpointRounding.AwayFromZero);
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

        return candidates;
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
            supersededCandidate.Revision = candidate.Revision + 1;
            supersededCandidate.ArchiveReason = null;
            supersededCandidate.ArchivedUtc = null;
            if (await discoveryRunCandidateStore.TryUpdateAsync(supersededCandidate, candidate.Revision, cancellationToken))
            {
                await discoveryRunCandidateDispositionStore.UpsertAsync(new DiscoveryRunCandidateDisposition
                {
                    Id = BuildDispositionId(run, supersededCandidate, DiscoveryRunCandidateStates.Superseded),
                    State = DiscoveryRunCandidateStates.Superseded,
                    ScopeFingerprint = DiscoveryRunScopePolicy.CreateFingerprint(run),
                    RequestedCategoryKeys = run.RequestedCategoryKeys.ToArray(),
                    Market = run.Market,
                    Locale = run.Locale,
                    NormalizedHost = DiscoveryRunCandidateIdentity.GetNormalizedHost(supersededCandidate),
                    NormalizedBaseUrl = DiscoveryRunCandidateIdentity.GetNormalizedBaseUrl(supersededCandidate),
                    NormalizedDisplayName = DiscoveryRunCandidateIdentity.GetNormalizedDisplayName(supersededCandidate),
                    AllowedMarkets = DiscoveryRunCandidateIdentity.NormalizeMarkets(supersededCandidate.AllowedMarkets),
                    SourceRunId = run.RunId,
                    SourceCandidateKey = supersededCandidate.CandidateKey,
                    SupersededByCandidateKey = acceptedCandidate.CandidateKey,
                    IsActive = true,
                    CreatedUtc = supersededCandidate.DecisionUtc ?? supersededCandidate.UpdatedUtc,
                    UpdatedUtc = supersededCandidate.DecisionUtc ?? supersededCandidate.UpdatedUtc
                }, cancellationToken);
            }
        }
    }

    private async Task<DiscoveryRunCandidateDisposition?> FindActiveDispositionAsync(string scopeFingerprint, SourceCandidateSearchResult candidate, CancellationToken cancellationToken)
    {
        var matches = await discoveryRunCandidateDispositionStore.FindActiveMatchesAsync(
            scopeFingerprint,
            DiscoveryRunCandidateIdentity.GetNormalizedHost(candidate),
            DiscoveryRunCandidateIdentity.GetNormalizedBaseUrl(candidate),
            DiscoveryRunCandidateIdentity.GetNormalizedDisplayName(candidate),
            candidate.AllowedMarkets,
            cancellationToken);

        return matches.FirstOrDefault();
    }

    private void RegisterAcceptedCandidate(DiscoveryRun run, DiscoveryRunCandidate candidate)
    {
        if (run.FirstAcceptedUtc is null && candidate.DecisionUtc is not null)
        {
            run.FirstAcceptedUtc = candidate.DecisionUtc;
            if (run.StartedUtc is not null)
            {
                run.TimeToFirstAcceptedCandidateMs = Math.Max(0L, (long)(candidate.DecisionUtc.Value - run.StartedUtc.Value).TotalMilliseconds);
            }
        }
    }

    private void EmitRunTelemetry(DiscoveryRun run)
    {
        if (run.CandidateThroughputPerMinute is not null)
        {
            ProductNormaliserTelemetry.DiscoveryCandidateThroughputPerMinute.Record((double)run.CandidateThroughputPerMinute.Value, new TagList { { "status", run.Status } });
        }

        if (run.AcceptanceRate is not null)
        {
            ProductNormaliserTelemetry.DiscoveryAcceptanceRate.Record((double)run.AcceptanceRate.Value, new TagList { { "status", run.Status } });
        }

        if (run.ManualReviewRate is not null)
        {
            ProductNormaliserTelemetry.DiscoveryManualReviewRate.Record((double)run.ManualReviewRate.Value, new TagList { { "status", run.Status } });
        }

        if (run.TimeToFirstAcceptedCandidateMs is not null)
        {
            ProductNormaliserTelemetry.DiscoveryTimeToFirstAcceptedMs.Record(run.TimeToFirstAcceptedCandidateMs.Value, new TagList { { "status", run.Status } });
        }
    }

    private void EnsureBudgetDiagnostics(DiscoveryRun run)
    {
        AppendUniqueDiagnostic(run.Diagnostics, new DiscoveryRunDiagnostic
        {
            Code = "search_budget",
            Severity = SourceCandidateDiscoveryDiagnostic.SeverityInfo,
            Title = "Search budget",
            Message = $"Search-provider budget is {run.SearchTimeoutBudgetMs}ms for this run."
        });
        AppendUniqueDiagnostic(run.Diagnostics, new DiscoveryRunDiagnostic
        {
            Code = "probe_budget",
            Severity = SourceCandidateDiscoveryDiagnostic.SeverityInfo,
            Title = "Probe budget",
            Message = $"Probe-fetch budget is {run.ProbeTimeoutBudgetMs}ms per candidate."
        });
        AppendUniqueDiagnostic(run.Diagnostics, new DiscoveryRunDiagnostic
        {
            Code = "llm_budget",
            Severity = SourceCandidateDiscoveryDiagnostic.SeverityInfo,
            Title = "LLM budget",
            Message = $"LLM verification budget is {run.LlmTimeoutBudgetMs}ms per candidate."
        });
    }

    private static string CreateHistoricalSuppressionMessage(DiscoveryRunCandidateDisposition disposition)
    {
        return string.Equals(disposition.State, DiscoveryRunCandidateStates.Dismissed, StringComparison.OrdinalIgnoreCase)
            ? $"Archived from active review because this candidate was previously dismissed in run '{disposition.SourceRunId}' for the same market, locale, and category scope. Restore it to review again."
            : $"Archived from active review because this candidate was previously superseded in run '{disposition.SourceRunId}' for the same market, locale, and category scope. Restore it to review again.";
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

    private static string DetermineCandidateState(SourceCandidateResult candidate, DiscoveryRun run, SourceOnboardingAutomationOptions automationOptions)
    {
        if (candidate.AutomationAssessment.EligibleForAutoAccept
            && !candidate.AlreadyRegistered
            && candidate.AllowedByGovernance
            && string.Equals(candidate.RecommendationStatus, SourceCandidateResult.RecommendationRecommended, StringComparison.OrdinalIgnoreCase)
            && run.AutoAcceptedCandidateCount < automationOptions.MaxAutoAcceptedCandidatesPerRun)
        {
            return DiscoveryRunCandidateStates.AutoAccepted;
        }

        return string.Equals(candidate.RecommendationStatus, SourceCandidateResult.RecommendationDoNotAccept, StringComparison.OrdinalIgnoreCase)
            ? DiscoveryRunCandidateStates.Failed
            : DiscoveryRunCandidateStates.Suggested;
    }

    private static DiscoveryRunDiagnostic MapDiagnostic(SourceCandidateDiscoveryDiagnostic diagnostic)
    {
        return new DiscoveryRunDiagnostic
        {
            Code = diagnostic.Code,
            Severity = diagnostic.Severity,
            Title = diagnostic.Title,
            Message = diagnostic.Message
        };
    }

    private static void AppendDiagnostics(List<DiscoveryRunDiagnostic> target, IEnumerable<SourceCandidateDiscoveryDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            AppendUniqueDiagnostic(target, MapDiagnostic(diagnostic));
        }
    }

    private static void AppendUniqueDiagnostic(List<DiscoveryRunDiagnostic> target, DiscoveryRunDiagnostic diagnostic)
    {
        if (target.Any(existing => string.Equals(existing.Code, diagnostic.Code, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        target.Add(diagnostic);
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