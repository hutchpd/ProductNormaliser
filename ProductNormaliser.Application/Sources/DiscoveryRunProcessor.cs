using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProductNormaliser.Application.Governance;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

public sealed class DiscoveryRunProcessor(
    IDiscoveryRunStore discoveryRunStore,
    IDiscoveryRunCandidateStore discoveryRunCandidateStore,
    ICrawlSourceStore crawlSourceStore,
    ISourceManagementService sourceManagementService,
    ICrawlGovernanceService crawlGovernanceService,
    ISourceCandidateSearchProvider sourceCandidateSearchProvider,
    ISourceCandidateProbeService sourceCandidateProbeService,
    IOptions<SourceOnboardingAutomationOptions> onboardingAutomationOptions,
    ILogger<DiscoveryRunProcessor>? logger = null) : IDiscoveryRunProcessor
{
    private readonly SourceCandidateDiscoveryEvaluator evaluator = new(onboardingAutomationOptions.Value);
    private readonly ILogger<DiscoveryRunProcessor> logger = logger ?? NullLogger<DiscoveryRunProcessor>.Instance;

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        var run = await discoveryRunStore.GetNextQueuedAsync(cancellationToken);
        if (run is null)
        {
            return false;
        }

        var utcNow = DateTime.UtcNow;
        run.Status = DiscoveryRunStatuses.Running;
        run.StartedUtc ??= utcNow;
        run.UpdatedUtc = utcNow;
        run.LastHeartbeatUtc = utcNow;
        run.StatusMessage = "Worker picked up the discovery run and is preparing search queries.";
        await discoveryRunStore.UpsertAsync(run, cancellationToken);

        try
        {
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
            var searchResponse = await sourceCandidateSearchProvider.SearchAsync(request, cancellationToken);
            run.SearchResultCount = searchResponse.Candidates.Count;
            run.Diagnostics = searchResponse.Diagnostics.Select(MapDiagnostic).ToList();
            await TouchRunAsync(run, cancellationToken);

            if (await TryRespectOperatorControlsAsync(run.RunId, cancellationToken))
            {
                return true;
            }

            await UpdateRunAsync(run, DiscoveryRunStageNames.CollapseAndDedupe, "Collapsing equivalent hosts and removing duplicate search hits.", cancellationToken);
            var collapsedCandidates = evaluator.CollapseEquivalentCandidates(searchResponse.Candidates);
            run.CollapsedCandidateCount = collapsedCandidates.Count;
            await TouchRunAsync(run, cancellationToken);

            foreach (var searchCandidate in collapsedCandidates)
            {
                var pendingCandidate = new DiscoveryRunCandidate
                {
                    Id = $"{run.RunId}:{searchCandidate.CandidateKey}",
                    RunId = run.RunId,
                    CandidateKey = string.IsNullOrWhiteSpace(searchCandidate.CandidateKey) ? searchCandidate.Host : searchCandidate.CandidateKey,
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
                    CreatedUtc = DateTime.UtcNow,
                    UpdatedUtc = DateTime.UtcNow,
                    StateMessage = "Waiting to be probed by the background worker."
                };

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
                if (storedCandidate is null)
                {
                    continue;
                }

                if (string.Equals(run.LlmStatus, "active", StringComparison.OrdinalIgnoreCase))
                {
                    await UpdateRunAsync(run, DiscoveryRunStageNames.LlmVerify, $"Running serial local verification for candidate {index + 1} of {collapsedCandidates.Count}.", cancellationToken);
                    storedCandidate.State = DiscoveryRunCandidateStates.AwaitingLlm;
                    storedCandidate.StateMessage = "Queued for serial local LLM verification. This stage intentionally runs with low concurrency.";
                }
                else
                {
                    await UpdateRunAsync(run, DiscoveryRunStageNames.Probe, $"Probing representative pages for candidate {index + 1} of {collapsedCandidates.Count}.", cancellationToken);
                    storedCandidate.State = DiscoveryRunCandidateStates.Probing;
                    storedCandidate.StateMessage = "Probing representative category and product pages.";
                }

                storedCandidate.UpdatedUtc = DateTime.UtcNow;
                await discoveryRunCandidateStore.UpsertAsync(storedCandidate, cancellationToken);

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
                        Message = $"Probing timed out for '{searchCandidate.DisplayName}'. The worker continued with reduced confidence for this candidate."
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

                var mappedCandidate = DiscoveryRunMapper.ToDocument(run.RunId, candidateResult, DetermineCandidateState(candidateResult), storedCandidate.CreatedUtc);
                mappedCandidate.StateMessage = mappedCandidate.State switch
                {
                    DiscoveryRunCandidateStates.AutoAccepted => "Marked for guarded auto-accept and publish.",
                    DiscoveryRunCandidateStates.Suggested => "Ready for operator review.",
                    _ => "Candidate did not clear guarded acceptance thresholds."
                };
                mappedCandidate.UpdatedUtc = DateTime.UtcNow;
                mappedCandidate.CreatedUtc = storedCandidate.CreatedUtc;
                await discoveryRunCandidateStore.UpsertAsync(mappedCandidate, cancellationToken);

                run.ProbeCompletedCount += 1;
                if (probe.LlmElapsedMs is > 0)
                {
                    run.LlmCompletedCount += 1;
                    run.LlmTotalElapsedMs += probe.LlmElapsedMs.Value;
                    run.LlmAverageElapsedMs = (long)Math.Round(run.LlmTotalElapsedMs / (double)run.LlmCompletedCount, MidpointRounding.AwayFromZero);
                }

                run.LlmQueueDepth = Math.Max(0, collapsedCandidates.Count - (index + 1));
                await TouchRunAsync(run, cancellationToken);
            }

            await UpdateRunAsync(run, DiscoveryRunStageNames.Score, "Scoring candidate confidence and weighting extraction signals.", cancellationToken);
            AppendDiagnostics(run.Diagnostics, evaluator.BuildProbeDiagnostics(evaluatedCandidates));
            AppendDiagnostics(run.Diagnostics, evaluator.BuildLlmDiagnostics(evaluatedCandidates));
            await TouchRunAsync(run, cancellationToken);

            await UpdateRunAsync(run, DiscoveryRunStageNames.Decide, "Assigning guarded decisions for suggested and auto-accepted candidates.", cancellationToken);
            var currentCandidates = await discoveryRunCandidateStore.ListByRunAsync(run.RunId, cancellationToken);
            run.SuggestedCandidateCount = currentCandidates.Count(candidate => string.Equals(candidate.State, DiscoveryRunCandidateStates.Suggested, StringComparison.OrdinalIgnoreCase));
            run.AutoAcceptedCandidateCount = currentCandidates.Count(candidate => string.Equals(candidate.State, DiscoveryRunCandidateStates.AutoAccepted, StringComparison.OrdinalIgnoreCase));
            await TouchRunAsync(run, cancellationToken);

            await UpdateRunAsync(run, DiscoveryRunStageNames.Publish, "Publishing auto-accepted candidates into the source registry.", cancellationToken);
            foreach (var candidate in currentCandidates.Where(candidate => string.Equals(candidate.State, DiscoveryRunCandidateStates.AutoAccepted, StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                if (await TryRespectOperatorControlsAsync(run.RunId, cancellationToken))
                {
                    return true;
                }

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
                    candidate.StateMessage = $"Auto-accepted and registered as source '{source.Id}'.";
                    candidate.DecisionUtc = DateTime.UtcNow;
                    candidate.UpdatedUtc = candidate.DecisionUtc.Value;
                    await discoveryRunCandidateStore.UpsertAsync(candidate, cancellationToken);
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Auto-accept publish failed for discovery run {RunId} candidate {CandidateKey}", run.RunId, candidate.CandidateKey);
                    candidate.PreviousState = candidate.State;
                    candidate.State = DiscoveryRunCandidateStates.Failed;
                    candidate.StateMessage = $"Auto-accept publish failed: {exception.Message}";
                    candidate.DecisionUtc = DateTime.UtcNow;
                    candidate.UpdatedUtc = candidate.DecisionUtc.Value;
                    await discoveryRunCandidateStore.UpsertAsync(candidate, cancellationToken);
                }
            }

            var publishedCandidates = await discoveryRunCandidateStore.ListByRunAsync(run.RunId, cancellationToken);
            run.PublishedCandidateCount = publishedCandidates.Count(candidate => !string.IsNullOrWhiteSpace(candidate.AcceptedSourceId));
            run.Status = DiscoveryRunStatuses.Completed;
            run.CompletedUtc = DateTime.UtcNow;
            run.StatusMessage = "Discovery run completed. Suggested and auto-accepted candidates are ready for review.";
            run.UpdatedUtc = run.CompletedUtc.Value;
            run.LastHeartbeatUtc = run.CompletedUtc.Value;
            await discoveryRunStore.UpsertAsync(run, cancellationToken);
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

    private static string DetermineCandidateState(SourceCandidateResult candidate)
    {
        if (candidate.AutomationAssessment.EligibleForAutoAccept
            && !candidate.AlreadyRegistered
            && candidate.AllowedByGovernance
            && string.Equals(candidate.RecommendationStatus, SourceCandidateResult.RecommendationRecommended, StringComparison.OrdinalIgnoreCase))
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
}