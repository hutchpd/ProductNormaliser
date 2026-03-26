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
            && !string.Equals(candidate.State, "archived", StringComparison.OrdinalIgnoreCase))
        .ToArray();

    public IReadOnlyList<DiscoveryRunCandidateDto> ArchivedCandidates => Candidates
        .Where(candidate => string.Equals(candidate.State, "dismissed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.State, "archived", StringComparison.OrdinalIgnoreCase))
        .ToArray();

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

    public async Task<IActionResult> OnPostAcceptCandidateAsync(string candidateKey, CancellationToken cancellationToken)
    {
        return await CandidateMutationAsync(() => adminApiClient.AcceptDiscoveryRunCandidateAsync(RunId, candidateKey, cancellationToken), $"Accepted candidate '{candidateKey}'.");
    }

    public async Task<IActionResult> OnPostDismissCandidateAsync(string candidateKey, CancellationToken cancellationToken)
    {
        return await CandidateMutationAsync(() => adminApiClient.DismissDiscoveryRunCandidateAsync(RunId, candidateKey, cancellationToken), $"Dismissed candidate '{candidateKey}'.");
    }

    public async Task<IActionResult> OnPostRestoreCandidateAsync(string candidateKey, CancellationToken cancellationToken)
    {
        return await CandidateMutationAsync(() => adminApiClient.RestoreDiscoveryRunCandidateAsync(RunId, candidateKey, cancellationToken), $"Restored candidate '{candidateKey}'.");
    }

    public string GetCandidateStatusTone(DiscoveryRunCandidateDto candidate)
    {
        return candidate.State switch
        {
            "auto_accepted" => "completed",
            "manually_accepted" => "completed",
            "suggested" => "pending",
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