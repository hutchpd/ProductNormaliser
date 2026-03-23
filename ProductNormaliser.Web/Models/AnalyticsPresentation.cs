using ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Models;

public static class AnalyticsPresentation
{
    public static string FormatPercent(decimal value)
    {
        return $"{decimal.Round(value, 0, MidpointRounding.AwayFromZero):0}%";
    }

    public static string FormatConfidence(decimal value)
    {
        return $"{decimal.Round(value * 100m, 0, MidpointRounding.AwayFromZero):0}%";
    }

    public static string GetPositiveTone(decimal value)
    {
        return value switch
        {
            >= 85m => "completed",
            >= 60m => "warning",
            > 0m => "danger",
            _ => "neutral"
        };
    }

    public static string GetInverseTone(decimal value)
    {
        return value switch
        {
            >= 60m => "danger",
            >= 25m => "warning",
            > 0m => "completed",
            _ => "neutral"
        };
    }

    public static string GetWidthStyle(decimal value)
    {
        var bounded = decimal.Clamp(value, 0m, 100m);
        return $"width: {bounded:0.##}%";
    }

    public static decimal GetChangeActivity(SourceQualitySnapshotDto snapshot)
    {
        return decimal.Clamp(100m - snapshot.SpecStabilityScore, 0m, 100m);
    }

    public static DisagreementMatrixModel BuildDisagreementMatrix(IReadOnlyList<SourceAttributeDisagreementDto> disagreements, int maxAttributes = 8, int maxSources = 6)
    {
        var columns = disagreements
            .GroupBy(item => item.SourceName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new { SourceName = group.Key, TotalComparisons = group.Sum(item => item.TotalComparisons), AverageRate = group.Average(item => item.DisagreementRate) })
            .OrderByDescending(item => item.TotalComparisons)
            .ThenByDescending(item => item.AverageRate)
            .ThenBy(item => item.SourceName, StringComparer.OrdinalIgnoreCase)
            .Take(maxSources)
            .Select(item => new DisagreementMatrixColumnModel { SourceName = item.SourceName })
            .ToArray();

        var columnNames = columns.Select(column => column.SourceName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rows = disagreements
            .Where(item => columnNames.Contains(item.SourceName))
            .GroupBy(item => item.AttributeKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                AttributeKey = group.Key,
                TotalDisagreements = group.Sum(item => item.TimesDisagreed),
                TotalComparisons = group.Sum(item => item.TotalComparisons),
                Cells = group.ToDictionary(item => item.SourceName, StringComparer.OrdinalIgnoreCase)
            })
            .OrderByDescending(item => item.TotalDisagreements)
            .ThenByDescending(item => item.TotalComparisons)
            .ThenBy(item => item.AttributeKey, StringComparer.OrdinalIgnoreCase)
            .Take(maxAttributes)
            .Select(item => new DisagreementMatrixRowModel
            {
                AttributeKey = item.AttributeKey,
                Cells = columns.Select(column =>
                {
                    if (!item.Cells.TryGetValue(column.SourceName, out var disagreement))
                    {
                        return new DisagreementMatrixCellModel();
                    }

                    return new DisagreementMatrixCellModel
                    {
                        HasData = true,
                        DisagreementRate = disagreement.DisagreementRate,
                        TimesDisagreed = disagreement.TimesDisagreed,
                        TotalComparisons = disagreement.TotalComparisons,
                        Tone = GetInverseTone(disagreement.DisagreementRate)
                    };
                }).ToArray()
            })
            .ToArray();

        return new DisagreementMatrixModel
        {
            Columns = columns,
            Rows = rows
        };
    }
}

public sealed class DisagreementMatrixModel
{
    public IReadOnlyList<DisagreementMatrixColumnModel> Columns { get; init; } = [];
    public IReadOnlyList<DisagreementMatrixRowModel> Rows { get; init; } = [];
}

public sealed class DisagreementMatrixColumnModel
{
    public string SourceName { get; init; } = string.Empty;
}

public sealed class DisagreementMatrixRowModel
{
    public string AttributeKey { get; init; } = string.Empty;
    public IReadOnlyList<DisagreementMatrixCellModel> Cells { get; init; } = [];
}

public sealed class DisagreementMatrixCellModel
{
    public bool HasData { get; init; }
    public decimal DisagreementRate { get; init; }
    public int TimesDisagreed { get; init; }
    public int TotalComparisons { get; init; }
    public string Tone { get; init; } = "neutral";
}
