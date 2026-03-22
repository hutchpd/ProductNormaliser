using ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Models;

public static class CrawlJobPresentation
{
    public static bool IsActiveStatus(string? status)
    {
        var normalized = NormalizeStatus(status);
        return normalized is "pending" or "running" or "cancel_requested";
    }

    public static bool IsCompletedStatus(string? status)
    {
        return NormalizeStatus(status) is "completed";
    }

    public static bool IsFailedStatus(string? status)
    {
        return NormalizeStatus(status) is "failed" or "completed_with_failures" or "cancelled";
    }

    public static StatusBadgeModel GetStatusBadge(string? status)
    {
        return NormalizeStatus(status) switch
        {
            "running" => new StatusBadgeModel { Text = "Running", Tone = "running" },
            "pending" => new StatusBadgeModel { Text = "Pending", Tone = "pending" },
            "cancel_requested" => new StatusBadgeModel { Text = "Cancel requested", Tone = "warning" },
            "cancelled" => new StatusBadgeModel { Text = "Cancelled", Tone = "neutral" },
            "completed" => new StatusBadgeModel { Text = "Completed", Tone = "completed" },
            "completed_with_failures" => new StatusBadgeModel { Text = "Completed with failures", Tone = "warning" },
            "failed" => new StatusBadgeModel { Text = "Failed", Tone = "danger" },
            _ => new StatusBadgeModel { Text = string.IsNullOrWhiteSpace(status) ? "Unknown" : status!, Tone = "neutral" }
        };
    }

    public static int GetProgressPercent(CrawlJobDto job)
    {
        if (job.TotalTargets <= 0)
        {
            return 0;
        }

        return Math.Min(100, (int)Math.Round((double)job.ProcessedTargets / job.TotalTargets * 100d, MidpointRounding.AwayFromZero));
    }

    public static string GetScopeSummary(CrawlJobDto job)
    {
        if (job.RequestedCategories.Count > 0)
        {
            return string.Join(", ", job.RequestedCategories);
        }

        if (job.RequestedSources.Count > 0)
        {
            return string.Join(", ", job.RequestedSources);
        }

        return string.Join(", ", job.RequestedProductIds);
    }

    public static string GetRequestedCategoriesSummary(CrawlJobDto job)
    {
        return job.RequestedCategories.Count == 0
            ? "No categories recorded"
            : string.Join(", ", job.RequestedCategories);
    }

    public static string GetRequestedSourcesSummary(CrawlJobDto job)
    {
        return job.RequestedSources.Count == 0
            ? "All matching sources"
            : string.Join(", ", job.RequestedSources);
    }

    private static string NormalizeStatus(string? status)
    {
        return string.IsNullOrWhiteSpace(status)
            ? string.Empty
            : status.Trim().Replace('-', '_').ToLowerInvariant();
    }
}