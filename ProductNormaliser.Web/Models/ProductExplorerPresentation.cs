using ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Models;

public static class ProductExplorerPresentation
{
    public static StatusBadgeModel GetFreshnessBadge(string? freshnessStatus, int freshnessAgeDays)
    {
        var normalized = Normalize(freshnessStatus);
        return normalized switch
        {
            "fresh" => new StatusBadgeModel { Text = freshnessAgeDays == 0 ? "Fresh today" : $"Fresh ({freshnessAgeDays}d)", Tone = "completed" },
            "aging" => new StatusBadgeModel { Text = $"Aging ({freshnessAgeDays}d)", Tone = "warning" },
            "stale" => new StatusBadgeModel { Text = $"Stale ({freshnessAgeDays}d)", Tone = "danger" },
            _ => new StatusBadgeModel { Text = freshnessAgeDays <= 0 ? "Unknown freshness" : $"{freshnessAgeDays}d old", Tone = "neutral" }
        };
    }

    public static StatusBadgeModel GetCompletenessBadge(decimal completenessScore, string? completenessStatus)
    {
        var normalized = Normalize(completenessStatus);
        var percent = FormatPercent(completenessScore);

        return normalized switch
        {
            "complete" => new StatusBadgeModel { Text = $"Complete {percent}", Tone = "completed" },
            "partial" => new StatusBadgeModel { Text = $"Partial {percent}", Tone = "warning" },
            "sparse" => new StatusBadgeModel { Text = $"Sparse {percent}", Tone = "danger" },
            _ => new StatusBadgeModel { Text = percent, Tone = "neutral" }
        };
    }

    public static StatusBadgeModel GetConflictBadge(bool hasConflict, int conflictAttributeCount)
    {
        return hasConflict
            ? new StatusBadgeModel { Text = $"{conflictAttributeCount} conflict{(conflictAttributeCount == 1 ? string.Empty : "s")}", Tone = "warning" }
            : new StatusBadgeModel { Text = "No conflicts", Tone = "completed" };
    }

    public static string FormatPercent(decimal value)
    {
        return $"{value:P0}";
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace('-', '_').ToLowerInvariant();
    }
}