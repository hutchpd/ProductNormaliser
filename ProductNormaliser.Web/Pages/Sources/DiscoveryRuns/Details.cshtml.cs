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
    private const string SnapshotTimestampKind = "Snapshot";
    private const string ExactTimestampKind = "Recorded";

    [BindProperty(SupportsGet = true, Name = "runId")]
    public string RunId { get; set; } = string.Empty;

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public DiscoveryRunDto? Run { get; private set; }

    public IReadOnlyList<DiscoveryRunCandidateDto> Candidates { get; private set; } = [];

    public bool ShouldAutoRefresh => Run is not null && DiscoveryRunPresentation.IsActiveStatus(Run.Status);

    public StatusBadgeModel StatusBadge => DiscoveryRunPresentation.GetStatusBadge(Run?.Status);

    public int ProgressPercent => Run is null ? 0 : DiscoveryRunPresentation.GetProgressPercent(Run);

    public IReadOnlyList<DiscoveryRunCandidateDto> ActiveCandidates => Candidates
        .Where(candidate => !string.Equals(candidate.State, "dismissed", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate.State, "archived", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate.State, "superseded", StringComparison.OrdinalIgnoreCase))
        .ToArray();

    public IReadOnlyList<DiscoveryRunCandidateDto> ArchivedCandidates => Candidates
        .Where(candidate => string.Equals(candidate.State, "dismissed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.State, "archived", StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.State, "superseded", StringComparison.OrdinalIgnoreCase))
        .ToArray();

    public IReadOnlyList<DiscoveryRunActivityEntryModel> ActivityLogEntries => BuildActivityLogEntries();

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
                candidate.StateMessage
            }
            .OfType<string>()
            .Concat(candidate.Reasons
            .Select(reason => reason.Message)
            .Concat(candidate.AutomationAssessment.BlockingReasons))
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();
    }

    public string GetArchiveActionLabel(DiscoveryRunCandidateDto candidate)
    {
        return CanRestoreCandidate(candidate)
            ? "Add back to review queue"
            : "Archived for reference";
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
            entries.Add(new DiscoveryRunActivityEntryModel
            {
                TimestampUtc = Run.UpdatedUtc,
                TimestampKind = SnapshotTimestampKind,
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

    private static string BuildArchivedCandidateSummary(DiscoveryRunCandidateDto candidate)
    {
        var reasons = new[]
            {
                candidate.ArchiveReason,
                candidate.StateMessage
            }
            .Concat(candidate.AutomationAssessment.BlockingReasons)
            .Concat(candidate.Reasons.Select(reason => reason.Message))
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var suffix = reasons.Length == 0
            ? "No specific archive reason was persisted for this snapshot."
            : string.Join(" ", reasons.Take(3));

        return $"{candidate.Host} was moved out of the active queue. {suffix}";
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
            return RedirectToPage(new { runId = run.RunId });
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
            return RedirectToPage(new { runId = RunId });
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
                Candidates = [];
                return;
            }

            Run = await adminApiClient.GetDiscoveryRunAsync(RunId, cancellationToken);
            if (Run is null)
            {
                ErrorMessage = $"Discovery run '{RunId}' was not found.";
                Candidates = [];
                return;
            }

            Candidates = await adminApiClient.GetDiscoveryRunCandidatesAsync(RunId, cancellationToken);
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to load discovery run {RunId}.", RunId);
            ErrorMessage = exception.Message;
            Run = null;
            Candidates = [];
        }
    }
}