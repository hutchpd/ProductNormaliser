using ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Models;

public static class SourceManagementPresentation
{
    public static StatusBadgeModel GetEnabledBadge(bool isEnabled)
    {
        return new StatusBadgeModel
        {
            Text = isEnabled ? "Enabled" : "Disabled",
            Tone = isEnabled ? "completed" : "danger"
        };
    }

    public static StatusBadgeModel GetReadinessBadge(SourceReadinessDto? readiness)
    {
        var normalized = Normalize(readiness?.Status);
        return normalized switch
        {
            "ready" => new StatusBadgeModel { Text = "Ready", Tone = "completed" },
            "limited" => new StatusBadgeModel { Text = "Limited", Tone = "warning" },
            "blocked" => new StatusBadgeModel { Text = "Blocked", Tone = "danger" },
            "unassigned" => new StatusBadgeModel { Text = "Unassigned", Tone = "neutral" },
            _ => new StatusBadgeModel { Text = string.IsNullOrWhiteSpace(readiness?.Status) ? "Unknown" : readiness!.Status, Tone = "neutral" }
        };
    }

    public static StatusBadgeModel GetHealthBadge(SourceHealthSummaryDto? health)
    {
        var normalized = Normalize(health?.Status);
        return normalized switch
        {
            "healthy" => new StatusBadgeModel { Text = "Healthy", Tone = "completed" },
            "watch" => new StatusBadgeModel { Text = "Watch", Tone = "warning" },
            "attention" => new StatusBadgeModel { Text = "Attention", Tone = "danger" },
            _ => new StatusBadgeModel { Text = "Unknown", Tone = "neutral" }
        };
    }

    public static StatusBadgeModel GetLastActivityBadge(SourceLastActivityDto? activity)
    {
        if (activity is null)
        {
            return new StatusBadgeModel { Text = "No recent crawl", Tone = "neutral" };
        }

        var normalized = Normalize(activity.Status);
        return normalized switch
        {
            "succeeded" or "success" or "completed" => new StatusBadgeModel { Text = "Last crawl succeeded", Tone = "completed" },
            "failed" => new StatusBadgeModel { Text = "Last crawl failed", Tone = "danger" },
            "running" or "processing" => new StatusBadgeModel { Text = "Crawl running", Tone = "running" },
            "queued" or "pending" => new StatusBadgeModel { Text = "Crawl queued", Tone = "pending" },
            _ => new StatusBadgeModel { Text = activity.Status, Tone = "neutral" }
        };
    }

    public static string FormatThrottlingProfile(SourceThrottlingPolicyDto policy)
    {
        return $"{policy.RequestsPerMinute} rpm, {policy.MaxConcurrentRequests} concurrent, {policy.MinDelayMs}-{policy.MaxDelayMs} ms";
    }

    public static string FormatHealthSummary(SourceHealthSummaryDto? health)
    {
        if (health is null || string.Equals(health.Status, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return "No recent quality snapshot.";
        }

        return $"Trust {AnalyticsPresentation.FormatPercent(health.TrustScore)}, coverage {AnalyticsPresentation.FormatPercent(health.CoveragePercent)}, crawl success {AnalyticsPresentation.FormatPercent(health.SuccessfulCrawlRate)}";
    }

    public static string FormatLastActivity(SourceLastActivityDto? activity)
    {
        if (activity is null)
        {
            return "No crawl activity recorded yet.";
        }

        var changeLabel = activity.HadMeaningfulChange ? "meaningful change detected" : "no meaningful change";
        return $"{activity.TimestampUtc:u}, {activity.DurationMs} ms, {activity.ExtractedProductCount} products, {changeLabel}";
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace('-', '_').ToLowerInvariant();
    }
}