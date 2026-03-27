using ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Models;

public static class DiscoveryRunPresentation
{
    public static bool IsActiveStatus(string? status)
    {
        var normalized = NormalizeStatus(status);
        return normalized is "queued" or "running" or "cancel_requested";
    }

    public static bool CanPause(string? status)
    {
        var normalized = NormalizeStatus(status);
        return normalized is "queued" or "running";
    }

    public static bool CanResume(string? status)
    {
        return NormalizeStatus(status) is "paused";
    }

    public static bool CanStop(string? status)
    {
        var normalized = NormalizeStatus(status);
        return normalized is "queued" or "running" or "paused";
    }

    public static StatusBadgeModel GetStatusBadge(string? status)
    {
        return NormalizeStatus(status) switch
        {
            "queued" => new StatusBadgeModel { Text = "Queued", Tone = "pending" },
            "running" => new StatusBadgeModel { Text = "Running", Tone = "running" },
            "recoverable" => new StatusBadgeModel { Text = "Recoverable", Tone = "warning" },
            "paused" => new StatusBadgeModel { Text = "Paused", Tone = "warning" },
            "cancel_requested" => new StatusBadgeModel { Text = "Cancel requested", Tone = "warning" },
            "cancelled" => new StatusBadgeModel { Text = "Cancelled", Tone = "neutral" },
            "completed" => new StatusBadgeModel { Text = "Completed", Tone = "completed" },
            "failed" => new StatusBadgeModel { Text = "Failed", Tone = "danger" },
            _ => new StatusBadgeModel { Text = string.IsNullOrWhiteSpace(status) ? "Unknown" : status!, Tone = "neutral" }
        };
    }

    public static int GetProgressPercent(DiscoveryRunDto run)
    {
        var total = Math.Max(run.CollapsedCandidateCount, run.MaxCandidates);
        if (total <= 0)
        {
            return 0;
        }

        var completed = Math.Max(run.ProbeCompletedCount, run.PublishedCandidateCount);
        return Math.Min(100, (int)Math.Round(completed / (double)total * 100d, MidpointRounding.AwayFromZero));
    }

    public static string GetStageLabel(string? stage)
    {
        return NormalizeStatus(stage) switch
        {
            "search" => "Search",
            "collapse_and_dedupe" => "Collapse and dedupe",
            "probe" => "Probe",
            "llm_verify" => "LLM verify",
            "score" => "Score",
            "decide" => "Decide",
            "publish" => "Publish",
            _ => string.IsNullOrWhiteSpace(stage) ? "Unknown" : stage!
        };
    }

    private static string NormalizeStatus(string? status)
    {
        return string.IsNullOrWhiteSpace(status)
            ? string.Empty
            : status.Trim().Replace('-', '_').ToLowerInvariant();
    }
}