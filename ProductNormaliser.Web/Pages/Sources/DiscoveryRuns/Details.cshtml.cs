using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Models;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Pages.Sources.DiscoveryRuns;

public sealed class DetailsModel(
    IProductNormaliserAdminApiClient adminApiClient,
    ILogger<DetailsModel> logger) : PageModel
{
    private static readonly TimeSpan QueuePickupWarningThreshold = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan MinimumHeartbeatWarningThreshold = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan HeartbeatBudgetSlack = TimeSpan.FromSeconds(15);
    private const string SnapshotTimestampKind = "Snapshot";
    private const string ExactTimestampKind = "Recorded";
    private const string ReviewPrioritySort = "review_priority";
    private const string UpdatedDescSort = "updated_desc";
    private const int DefaultCandidatePageSize = 12;
    private const int DefaultArchivedPageSize = 8;

    [BindProperty(SupportsGet = true, Name = "runId")]
    public string RunId { get; set; } = string.Empty;

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public DiscoveryRunDto? Run { get; private set; }

    [BindProperty(SupportsGet = true)]
    public int CandidatePage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int CandidatePageSize { get; set; } = DefaultCandidatePageSize;

    [BindProperty(SupportsGet = true)]
    public string CandidateSort { get; set; } = ReviewPrioritySort;

    [BindProperty(SupportsGet = true)]
    public int ArchivedPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int ArchivedPageSize { get; set; } = DefaultArchivedPageSize;

    [BindProperty(SupportsGet = true)]
    public string ArchivedSort { get; set; } = UpdatedDescSort;

    public DiscoveryRunCandidatePageDto ActiveCandidatePage { get; private set; } = new();

    public DiscoveryRunCandidatePageDto ArchivedCandidatePage { get; private set; } = new();

    public IReadOnlyList<DiscoveryRunCandidateDto> Candidates => ActiveCandidates.Concat(ArchivedCandidates).ToArray();

    public bool ShouldAutoRefresh => Run is not null && DiscoveryRunPresentation.IsActiveStatus(Run.Status);

    public StatusBadgeModel StatusBadge => DiscoveryRunPresentation.GetStatusBadge(Run?.Status);

    public int ProgressPercent => Run is null ? 0 : DiscoveryRunPresentation.GetProgressPercent(Run);

    public IReadOnlyList<DiscoveryRunCandidateDto> ActiveCandidates => ActiveCandidatePage.Items;

    public IReadOnlyList<DiscoveryRunCandidateDto> ArchivedCandidates => ArchivedCandidatePage.Items;

    public DiscoveryRunCandidateRunSummaryDto CandidateSummary
    {
        get
        {
            if (ActiveCandidatePage.Summary.RunCandidateCount > 0)
            {
                return ActiveCandidatePage.Summary;
            }

            return ArchivedCandidatePage.Summary;
        }
    }

    public int SearchTimeoutCount => Run?.Diagnostics.Count(IsSearchTimeoutDiagnostic) ?? 0;

    public int ProbeTimeoutCandidateCount => CandidateSummary.ProbeTimeoutCandidateCount;

    public int RepresentativePageFetchFailureCandidateCount => CandidateSummary.RepresentativePageFetchFailureCandidateCount;

    public int RepresentativeCategoryFetchFailureCount => CandidateSummary.RepresentativeCategoryFetchFailureCount;

    public int RepresentativeProductFetchFailureCount => CandidateSummary.RepresentativeProductFetchFailureCount;

    public int LlmTimeoutCandidateCount => CandidateSummary.LlmTimeoutCandidateCount;

    public int LlmMeasuredCandidateCount => CandidateSummary.LlmMeasuredCandidateCount;

    public int LlmBudgetProbeCappedCandidateCount => CandidateSummary.LlmBudgetProbeCappedCandidateCount;

    public long? AverageLlmBudgetMs => CandidateSummary.AverageLlmBudgetMs;

    public decimal? AverageLlmBudgetUtilizationPercent => CandidateSummary.AverageLlmBudgetUtilizationPercent;

    public bool HasLlmBudgetMeasurements => LlmMeasuredCandidateCount > 0;

    public string ConfiguredLlmBudgetDisplay => Run?.LlmTimeoutBudgetMs is > 0
        ? FormatDuration(Run.LlmTimeoutBudgetMs.Value)
        : "n/a";

    public string AverageLlmBudgetDisplay => AverageLlmBudgetMs is > 0
        ? FormatDuration(AverageLlmBudgetMs.Value)
        : "n/a";

    public string AverageLlmBudgetUtilizationDisplay => AverageLlmBudgetUtilizationPercent is null
        ? "n/a"
        : $"{AverageLlmBudgetUtilizationPercent.Value:0.#}%";

    public string WorstCaseSerialLlmLaneDisplay => Run?.LlmTimeoutBudgetMs is > 0
        ? FormatDuration(Run.LlmTimeoutBudgetMs.Value * Math.Max(1, Run.MaxCandidates))
        : "n/a";

    public IReadOnlyList<DiscoveryRunCandidateBlockerSummaryDto> AutoAcceptBlockers => CandidateSummary.AutoAcceptBlockers;

    public bool HasAutoAcceptBlockers => AutoAcceptBlockers.Count > 0;

    public bool HasActiveCandidatePages => ActiveCandidatePage.TotalPages > 1;

    public bool HasArchivedCandidatePages => ArchivedCandidatePage.TotalPages > 1;

    public IReadOnlyList<DiscoveryRunActivityEntryModel> ActivityLogEntries => BuildActivityLogEntries();

    public IReadOnlyList<DiscoveryRunActivityEntryModel> SearchLogEntries => BuildSearchLogEntries();

    public string LastHeartbeatDisplay => Run?.LastHeartbeatUtc?.ToString("u") ?? "No worker heartbeat persisted yet";

    public string? WorkerLivenessWarning => Run is null ? null : BuildWorkerLivenessWarning(Run, DateTime.UtcNow);

    public bool HasWorkerLivenessWarning => !string.IsNullOrWhiteSpace(WorkerLivenessWarning);

    public string GetAutoAcceptBlockerCoverage(DiscoveryRunCandidateBlockerSummaryDto blocker)
    {
        var totalCandidates = Math.Max(1, CandidateSummary.RunCandidateCount);
        var percentage = decimal.Round(blocker.Count * 100m / totalCandidates, 1, MidpointRounding.AwayFromZero);
        return $"{blocker.Count} candidate(s), {percentage:0.#}% of this run";
    }

    public PageHeroModel Hero => Run is null
        ? new PageHeroModel
        {
            Eyebrow = "Discovery Run",
            Title = "Source candidate discovery",
            Description = "Monitor persisted source candidate discovery runs and take action on suggested hosts.",
            Metrics = []
        }
        : new PageHeroModel
        {
            Eyebrow = "Discovery Run",
            Title = Run.RunId,
            Description = "This page follows the persisted background discovery run, including stage progress, serial local verification throughput, and candidate decisions.",
            Metrics =
            [
                new HeroMetricModel { Label = "Stage", Value = DiscoveryRunPresentation.GetStageLabel(Run.CurrentStage) },
                new HeroMetricModel { Label = "Progress", Value = $"{ProgressPercent}%" },
                new HeroMetricModel { Label = "Suggested", Value = Run.SuggestedCandidateCount.ToString() },
                new HeroMetricModel { Label = "Published", Value = Run.PublishedCandidateCount.ToString() }
            ]
        };

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostPauseAsync(CancellationToken cancellationToken)
    {
        return await RunMutationAsync(() => adminApiClient.PauseDiscoveryRunAsync(RunId, cancellationToken), "Paused discovery run");
    }

    public async Task<IActionResult> OnPostResumeAsync(CancellationToken cancellationToken)
    {
        return await RunMutationAsync(() => adminApiClient.ResumeDiscoveryRunAsync(RunId, cancellationToken), "Resumed discovery run");
    }

    public async Task<IActionResult> OnPostStopAsync(CancellationToken cancellationToken)
    {
        return await RunMutationAsync(() => adminApiClient.StopDiscoveryRunAsync(RunId, cancellationToken), "Stopped discovery run");
    }

    public async Task<IActionResult> OnPostAcceptCandidateAsync(string candidateKey, int expectedRevision, CancellationToken cancellationToken)
    {
        return await CandidateMutationAsync(() => adminApiClient.AcceptDiscoveryRunCandidateAsync(RunId, candidateKey, expectedRevision, cancellationToken), $"Accepted candidate '{candidateKey}'.");
    }

    public async Task<IActionResult> OnPostDismissCandidateAsync(string candidateKey, int expectedRevision, CancellationToken cancellationToken)
    {
        return await CandidateMutationAsync(() => adminApiClient.DismissDiscoveryRunCandidateAsync(RunId, candidateKey, expectedRevision, cancellationToken), $"Dismissed candidate '{candidateKey}'.");
    }

    public async Task<IActionResult> OnPostRestoreCandidateAsync(string candidateKey, int expectedRevision, CancellationToken cancellationToken)
    {
        return await CandidateMutationAsync(() => adminApiClient.RestoreDiscoveryRunCandidateAsync(RunId, candidateKey, expectedRevision, cancellationToken), $"Restored candidate '{candidateKey}'.");
    }

    public string GetCandidateStatusTone(DiscoveryRunCandidateDto candidate)
    {
        return candidate.State switch
        {
            "auto_accepted" => "completed",
            "manually_accepted" => "completed",
            "suggested" => "pending",
            "superseded" => "neutral",
            "failed" => "danger",
            _ => "neutral"
        };
    }

    public string GetDiscoveryDiagnosticNoticeClass(SourceCandidateDiscoveryDiagnosticDto diagnostic)
    {
        return diagnostic.Severity switch
        {
            "error" => "notice error",
            "warning" => "notice warning",
            _ => "notice info"
        };
    }

    public bool CanAcceptCandidate(DiscoveryRunCandidateDto candidate)
    {
        return string.Equals(candidate.State, "suggested", StringComparison.OrdinalIgnoreCase);
    }

    public bool CanDismissCandidate(DiscoveryRunCandidateDto candidate)
    {
        return string.Equals(candidate.State, "suggested", StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.State, "failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.State, "archived", StringComparison.OrdinalIgnoreCase);
    }

    public bool CanRestoreCandidate(DiscoveryRunCandidateDto candidate)
    {
        return string.Equals(candidate.State, "dismissed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.State, "archived", StringComparison.OrdinalIgnoreCase);
    }

    public string GetActivityEntryClass(DiscoveryRunActivityEntryModel entry)
    {
        return entry.Tone switch
        {
            "error" => "activity-log-entry danger",
            "warning" => "activity-log-entry warning",
            "success" => "activity-log-entry success",
            _ => "activity-log-entry info"
        };
    }

    public IReadOnlyList<string> GetCandidateReasonSummary(DiscoveryRunCandidateDto candidate)
    {
        return new[]
            {
                candidate.ArchiveReason,
                GetCandidateDecisionSummary(candidate)
            }
            .OfType<string>()
            .Concat(GetCandidateBlockingReasons(candidate))
            .Concat(candidate.Reasons.Select(reason => reason.Message))
            .Concat(GetCandidateSupportingReasons(candidate))
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();
    }

    public string GetCandidateDecisionSummary(DiscoveryRunCandidateDto candidate)
    {
        if (!string.IsNullOrWhiteSpace(candidate.StateMessage))
        {
            return candidate.StateMessage!;
        }

        return candidate.State switch
        {
            "auto_accepted" => "Auto-accepted and published because unattended policy stayed green at decision time.",
            "manually_accepted" => "Accepted by an operator after review.",
            "suggested" => "Suggested for operator review.",
            "failed" => "Candidate did not clear guarded acceptance policy.",
            "dismissed" => "Dismissed from the active review queue.",
            "superseded" when !string.IsNullOrWhiteSpace(candidate.SupersededByCandidateKey) => $"Superseded by {candidate.SupersededByCandidateKey}.",
            "superseded" => "Superseded by another candidate in this run.",
            "archived" => "Archived from the active review queue.",
            _ => "Decision summary is unavailable for this candidate snapshot."
        };
    }

    public IReadOnlyList<string> GetCandidateSupportingReasons(DiscoveryRunCandidateDto candidate)
    {
        return GetProcessorSupportingReasons(candidate)
            .Concat(candidate.AutomationAssessment.SupportingReasons)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<string> GetCandidateBlockingReasons(DiscoveryRunCandidateDto candidate)
    {
        return GetProcessorBlockingReasons(candidate)
            .Concat(new[] { candidate.GovernanceWarning })
            .OfType<string>()
            .Concat(candidate.AutomationAssessment.BlockingReasons)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string GetArchiveActionLabel(DiscoveryRunCandidateDto candidate)
    {
        return CanRestoreCandidate(candidate)
            ? "Add back to review queue"
            : "Archived for reference";
    }

    public string GetLlmBudgetProbeCapSummary()
    {
        if (LlmBudgetProbeCappedCandidateCount <= 0)
        {
            return "No measured candidates had their LLM budget reduced by earlier probe work.";
        }

        return $"{LlmBudgetProbeCappedCandidateCount} of {Math.Max(1, LlmMeasuredCandidateCount)} measured candidate(s) reached the LLM stage with a reduced budget because representative fetches had already consumed part of the end-to-end probe allowance.";
    }

    private IReadOnlyList<DiscoveryRunActivityEntryModel> BuildActivityLogEntries()
    {
        if (Run is null)
        {
            return [];
        }

        var entries = new List<DiscoveryRunActivityEntryModel>
        {
            new()
            {
                TimestampUtc = Run.CreatedUtc,
                TimestampKind = ExactTimestampKind,
                Tone = "info",
                Title = "Discovery requested",
                Message = BuildDiscoveryRequestSummary(Run)
            }
        };

        if (Run.StartedUtc is { } startedUtc)
        {
            entries.Add(new DiscoveryRunActivityEntryModel
            {
                TimestampUtc = startedUtc,
                TimestampKind = ExactTimestampKind,
                Tone = "info",
                Title = "Worker picked up the run",
                Message = $"Background processing started in the {ProductNormaliser.Web.Models.DiscoveryRunPresentation.GetStageLabel(Run.CurrentStage).ToLowerInvariant()} stage."
            });
        }

        if (TryGetSearchCompletedUtc(Run, out var searchCompletedUtc))
        {
            entries.Add(new DiscoveryRunActivityEntryModel
            {
                TimestampUtc = searchCompletedUtc,
                TimestampKind = ExactTimestampKind,
                Tone = "success",
                Title = "Search completed",
                Message = $"Search returned {Run.SearchResultCount} raw result{(Run.SearchResultCount == 1 ? string.Empty : "s")} and collapsed them into {Run.CollapsedCandidateCount} candidate slot{(Run.CollapsedCandidateCount == 1 ? string.Empty : "s")}."
            });
        }
        else if (Run.SearchResultCount > 0 || Run.CollapsedCandidateCount > 0)
        {
            entries.Add(new DiscoveryRunActivityEntryModel
            {
                TimestampUtc = Run.UpdatedUtc,
                TimestampKind = SnapshotTimestampKind,
                Tone = "info",
                Title = "Search snapshot updated",
                Message = $"The latest run snapshot shows {Run.SearchResultCount} raw result{(Run.SearchResultCount == 1 ? string.Empty : "s")} and {Run.CollapsedCandidateCount} collapsed candidate slot{(Run.CollapsedCandidateCount == 1 ? string.Empty : "s")}."
            });
        }

        if (Run.ProbeCompletedCount > 0)
        {
            entries.Add(new DiscoveryRunActivityEntryModel
            {
                TimestampUtc = Run.UpdatedUtc,
                TimestampKind = SnapshotTimestampKind,
                Tone = "info",
                Title = "Representative probing updated",
                Message = $"{Run.ProbeCompletedCount} candidate slot{(Run.ProbeCompletedCount == 1 ? string.Empty : "s")} have representative page probe results in the current snapshot."
            });
        }

        if (Run.LlmQueueDepth > 0 || Run.LlmCompletedCount > 0)
        {
            entries.Add(new DiscoveryRunActivityEntryModel
            {
                TimestampUtc = Run.UpdatedUtc,
                TimestampKind = SnapshotTimestampKind,
                Tone = "info",
                Title = "Verification lane updated",
                Message = $"The local verification queue currently shows {Run.LlmQueueDepth} waiting and {Run.LlmCompletedCount} completed."
            });
        }

        foreach (var diagnostic in Run.Diagnostics)
        {
            if (IsSearchProgressDiagnostic(diagnostic))
            {
                continue;
            }

            entries.Add(new DiscoveryRunActivityEntryModel
            {
                TimestampUtc = diagnostic.RecordedUtc ?? Run.UpdatedUtc,
                TimestampKind = diagnostic.RecordedUtc is null ? SnapshotTimestampKind : ExactTimestampKind,
                Tone = diagnostic.Severity,
                Title = diagnostic.Title,
                Message = diagnostic.Message,
                Code = diagnostic.Code
            });
        }

        foreach (var candidate in ArchivedCandidates)
        {
            entries.Add(new DiscoveryRunActivityEntryModel
            {
                TimestampUtc = candidate.ArchivedUtc ?? Run.UpdatedUtc,
                TimestampKind = candidate.ArchivedUtc is null ? SnapshotTimestampKind : ExactTimestampKind,
                Tone = string.Equals(candidate.State, "superseded", StringComparison.OrdinalIgnoreCase) ? "info" : "warning",
                Title = BuildArchivedCandidateTitle(candidate),
                Message = BuildArchivedCandidateSummary(candidate)
            });
        }

        return entries
            .OrderByDescending(entry => entry.TimestampUtc)
            .ThenBy(entry => entry.TimestampKind, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<DiscoveryRunActivityEntryModel> BuildSearchLogEntries()
    {
        if (Run is null)
        {
            return [];
        }

        return Run.Diagnostics
            .Where(IsSearchProgressDiagnostic)
            .Select(diagnostic => new DiscoveryRunActivityEntryModel
            {
                TimestampUtc = diagnostic.RecordedUtc ?? Run.UpdatedUtc,
                TimestampKind = diagnostic.RecordedUtc is null ? SnapshotTimestampKind : ExactTimestampKind,
                Tone = diagnostic.Severity,
                Title = diagnostic.Title,
                Message = diagnostic.Message,
                Code = diagnostic.Code
            })
            .OrderByDescending(entry => entry.TimestampUtc)
            .ThenBy(entry => entry.TimestampKind, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool TryGetSearchCompletedUtc(DiscoveryRunDto run, out DateTime timestampUtc)
    {
        if (run.SearchElapsedMs is not { } searchElapsedMs)
        {
            timestampUtc = default;
            return false;
        }

        var baselineUtc = run.StartedUtc ?? run.CreatedUtc;
        timestampUtc = baselineUtc.AddMilliseconds(searchElapsedMs);
        return true;
    }

    private static string FormatDuration(long durationMs)
    {
        return durationMs >= 1000
            ? $"{durationMs / 1000d:0.#}s"
            : $"{durationMs}ms";
    }

    private static string? BuildWorkerLivenessWarning(DiscoveryRunDto run, DateTime utcNow)
    {
        if (string.Equals(run.Status, "queued", StringComparison.OrdinalIgnoreCase))
        {
            var queuedAge = utcNow - Max(run.UpdatedUtc, run.CreatedUtc);
            if (queuedAge >= QueuePickupWarningThreshold)
            {
                return $"This run has remained queued for {FormatAge(queuedAge)} without worker pickup. Worker capacity may be unavailable or the worker process may be offline.";
            }

            return null;
        }

        if (!string.Equals(run.Status, "running", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(run.Status, "cancel_requested", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var heartbeatUtc = run.LastHeartbeatUtc ?? run.UpdatedUtc;
        var heartbeatAge = utcNow - heartbeatUtc;
        var warningThreshold = GetHeartbeatWarningThreshold(run);
        if (heartbeatAge < warningThreshold)
        {
            return null;
        }

        return $"The worker has not heartbeated for {FormatAge(heartbeatAge)} while this run remains active. The {DiscoveryRunPresentation.GetStageLabel(run.CurrentStage)} stage may be stalled or the worker may be offline.";
    }

    private static TimeSpan GetHeartbeatWarningThreshold(DiscoveryRunDto run)
    {
        var stageBudgetMs = NormalizeStageBudget(run);
        if (stageBudgetMs is > 0)
        {
            return TimeSpan.FromMilliseconds(stageBudgetMs.Value) + HeartbeatBudgetSlack;
        }

        return MinimumHeartbeatWarningThreshold;
    }

    private static long? NormalizeStageBudget(DiscoveryRunDto run)
    {
        return run.CurrentStage switch
        {
            "search" => run.SearchTimeoutBudgetMs,
            "probe" => run.ProbeTimeoutBudgetMs,
            "llm_verify" => Math.Max(run.ProbeTimeoutBudgetMs ?? 0L, run.LlmTimeoutBudgetMs ?? 0L),
            _ => null
        };
    }

    private static DateTime Max(DateTime left, DateTime right)
    {
        return left >= right ? left : right;
    }

    private static string FormatAge(TimeSpan elapsed)
    {
        elapsed = elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;

        if (elapsed.TotalHours >= 1)
        {
            return $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m";
        }

        if (elapsed.TotalMinutes >= 1)
        {
            return $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";
        }

        return elapsed.TotalSeconds >= 1
            ? $"{Math.Max(1, (int)Math.Round(elapsed.TotalSeconds, MidpointRounding.AwayFromZero))}s"
            : "<1s";
    }

    private static bool IsSearchTimeoutDiagnostic(SourceCandidateDiscoveryDiagnosticDto diagnostic)
    {
        return string.Equals(diagnostic.Code, "search_timeout", StringComparison.OrdinalIgnoreCase)
            || string.Equals(diagnostic.Code, "search_provider_timeout", StringComparison.OrdinalIgnoreCase)
            || string.Equals(diagnostic.Code, "search.provider.timeout", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSearchProgressDiagnostic(SourceCandidateDiscoveryDiagnosticDto diagnostic)
    {
        return diagnostic.Code.StartsWith("search_query_started_", StringComparison.OrdinalIgnoreCase)
            || diagnostic.Code.StartsWith("search_query_results_", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildDiscoveryRequestSummary(DiscoveryRunDto run)
    {
        var market = string.IsNullOrWhiteSpace(run.Market) ? "any market" : run.Market;
        var locale = string.IsNullOrWhiteSpace(run.Locale) ? "any locale" : run.Locale;
        var brandHints = run.BrandHints.Count == 0
            ? "no brand hints"
            : $"brand hints: {string.Join(", ", run.BrandHints)}";

        return $"Queued search for {string.Join(", ", run.RequestedCategoryKeys)} in {market} / {locale} with up to {run.MaxCandidates} candidates and {brandHints}.";
    }

    private static string BuildArchivedCandidateTitle(DiscoveryRunCandidateDto candidate)
    {
        return candidate.State switch
        {
            "dismissed" => $"Dismissed {candidate.DisplayName}",
            "superseded" => $"Superseded {candidate.DisplayName}",
            _ => $"Archived {candidate.DisplayName}"
        };
    }

    private string BuildArchivedCandidateSummary(DiscoveryRunCandidateDto candidate)
    {
        var reasons = new[]
            {
                candidate.ArchiveReason,
                GetCandidateDecisionSummary(candidate)
            }
            .Concat(GetCandidateBlockingReasons(candidate))
            .Concat(GetCandidateSupportingReasons(candidate))
            .Concat(candidate.Reasons.Select(reason => reason.Message))
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var suffix = reasons.Length == 0
            ? "No specific archive reason was persisted for this snapshot."
            : string.Join(" ", reasons.Take(3));

        return $"{candidate.Host} was moved out of the active queue. {suffix}";
    }

    private static IEnumerable<string> GetProcessorSupportingReasons(DiscoveryRunCandidateDto candidate)
    {
        if (string.Equals(candidate.State, "auto_accepted", StringComparison.OrdinalIgnoreCase))
        {
            yield return "Recommendation stayed recommended and no registry duplicate blocked unattended publication.";
            yield return "The run still had auto-accept capacity when this candidate was decided.";
        }

        if (string.Equals(candidate.State, "manually_accepted", StringComparison.OrdinalIgnoreCase))
        {
            yield return "An operator accepted this candidate after review.";
        }

        if (string.Equals(candidate.State, "suggested", StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.AutomationAssessment.RequestedMode, "operator_assisted", StringComparison.OrdinalIgnoreCase))
        {
            yield return "This run is configured for operator-assisted review only.";
        }

        if (string.Equals(candidate.State, "suggested", StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.AutomationAssessment.RequestedMode, "suggest_accept", StringComparison.OrdinalIgnoreCase))
        {
            yield return "This run is configured to suggest strong candidates rather than auto-publish them.";
        }

        if (string.Equals(candidate.State, "suggested", StringComparison.OrdinalIgnoreCase)
            && candidate.AutomationAssessment.EligibleForSuggestion
            && !candidate.AutomationAssessment.EligibleForAutoAccept)
        {
            yield return "The candidate cleared suggestion policy even though unattended publication policy still had blockers.";
        }
    }

    private static IEnumerable<string> GetProcessorBlockingReasons(DiscoveryRunCandidateDto candidate)
    {
        if (string.Equals(candidate.State, "suggested", StringComparison.OrdinalIgnoreCase) && candidate.AlreadyRegistered)
        {
            yield return "This host is already registered, so discovery did not auto-publish a duplicate source.";
        }

        if (string.Equals(candidate.State, "suggested", StringComparison.OrdinalIgnoreCase) && candidate.AutomationAssessment.EligibleForAutoAccept)
        {
            yield return "This candidate cleared auto-accept guardrails, but the run had already consumed its auto-accept allowance.";
        }

        if (string.Equals(candidate.State, "failed", StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.RecommendationStatus, "do_not_accept", StringComparison.OrdinalIgnoreCase))
        {
            yield return "Recommendation state resolved to do_not_accept, so the candidate stayed out of the publish queue.";
        }

        if (!candidate.AllowedByGovernance && string.IsNullOrWhiteSpace(candidate.GovernanceWarning))
        {
            yield return "Governance rejected this candidate.";
        }
    }

    public sealed class DiscoveryRunActivityEntryModel
    {
        public DateTime TimestampUtc { get; init; }
        public string TimestampKind { get; init; } = SnapshotTimestampKind;
        public string Tone { get; init; } = "info";
        public string Title { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string? Code { get; init; }
    }

    private async Task<IActionResult> RunMutationAsync(Func<Task<DiscoveryRunDto>> action, string message)
    {
        try
        {
            var run = await action();
            StatusMessage = $"{message} '{run.RunId}'.";
            return RedirectToPage(new { runId = run.RunId, CandidatePage, CandidatePageSize, CandidateSort, ArchivedPage, ArchivedPageSize, ArchivedSort });
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to mutate discovery run {RunId}.", RunId);
            ErrorMessage = exception.Message;
            await LoadAsync(CancellationToken.None);
            return Page();
        }
    }

    private async Task<IActionResult> CandidateMutationAsync(Func<Task<DiscoveryRunCandidateDto>> action, string message)
    {
        try
        {
            await action();
            StatusMessage = message;
            return RedirectToPage(new { runId = RunId, CandidatePage, CandidatePageSize, CandidateSort, ArchivedPage, ArchivedPageSize, ArchivedSort });
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to mutate discovery run candidate for run {RunId}.", RunId);
            ErrorMessage = exception.Message;
            await LoadAsync(CancellationToken.None);
            return Page();
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(RunId))
            {
                ErrorMessage = "A discovery run id is required.";
                Run = null;
                ActiveCandidatePage = new DiscoveryRunCandidatePageDto();
                ArchivedCandidatePage = new DiscoveryRunCandidatePageDto();
                return;
            }

            Run = await adminApiClient.GetDiscoveryRunAsync(RunId, cancellationToken);
            if (Run is null)
            {
                ErrorMessage = $"Discovery run '{RunId}' was not found.";
                ActiveCandidatePage = new DiscoveryRunCandidatePageDto();
                ArchivedCandidatePage = new DiscoveryRunCandidatePageDto();
                return;
            }

            await LoadCandidatePagesAsync(cancellationToken);
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to load discovery run {RunId}.", RunId);
            ErrorMessage = exception.Message;
            Run = null;
            ActiveCandidatePage = new DiscoveryRunCandidatePageDto();
            ArchivedCandidatePage = new DiscoveryRunCandidatePageDto();
        }
    }

    private async Task LoadCandidatePagesAsync(CancellationToken cancellationToken)
    {
        ActiveCandidatePage = await adminApiClient.GetDiscoveryRunCandidatesAsync(
            RunId,
            new DiscoveryRunCandidateQueryDto
            {
                StateFilter = "active",
                Sort = CandidateSort,
                Page = CandidatePage,
                PageSize = CandidatePageSize
            },
            cancellationToken);

        CandidateSort = ActiveCandidatePage.Sort;
        CandidatePage = ActiveCandidatePage.Page;
        CandidatePageSize = ActiveCandidatePage.PageSize;

        ArchivedCandidatePage = await adminApiClient.GetDiscoveryRunCandidatesAsync(
            RunId,
            new DiscoveryRunCandidateQueryDto
            {
                StateFilter = "archived",
                Sort = ArchivedSort,
                Page = ArchivedPage,
                PageSize = ArchivedPageSize
            },
            cancellationToken);

        ArchivedSort = ArchivedCandidatePage.Sort;
        ArchivedPage = ArchivedCandidatePage.Page;
        ArchivedPageSize = ArchivedCandidatePage.PageSize;
    }
}