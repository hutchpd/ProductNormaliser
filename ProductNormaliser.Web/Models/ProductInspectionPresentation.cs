using System.Text.Json;
using ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Models;

public static class ProductInspectionPresentation
{
    public static IReadOnlyList<ProductSourceComparisonColumnModel> GetSourceComparisonColumns(ProductDetailDto product)
    {
        return product.SourceProducts
            .OrderBy(source => source.SourceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(source => source.Title, StringComparer.OrdinalIgnoreCase)
            .Select(source => new ProductSourceComparisonColumnModel
            {
                SourceProductId = source.Id,
                SourceName = source.SourceName,
                Title = source.Title ?? source.SourceName,
                ModelNumber = source.ModelNumber,
                Gtin = source.Gtin,
                SourceUrl = source.SourceUrl
            })
            .ToArray();
    }

    public static IReadOnlyList<ProductSourceComparisonRowModel> GetSourceComparisonRows(ProductDetailDto product)
    {
        var columns = GetSourceComparisonColumns(product);

        return product.Attributes
            .OrderBy(attribute => attribute.AttributeKey, StringComparer.OrdinalIgnoreCase)
            .Select(attribute => new ProductSourceComparisonRowModel
            {
                AttributeKey = attribute.AttributeKey,
                CanonicalValue = FormatValue(attribute.Value, attribute.Unit),
                HasConflict = HasDisagreement(attribute, product),
                EvidenceCount = attribute.Evidence.Count,
                Cells = columns.Select(column => BuildSourceComparisonCell(product, attribute, column)).ToArray()
            })
            .ToArray();
    }

    public static IReadOnlyList<ProductEvidenceInspectorRowModel> GetEvidenceInspectorRows(ProductDetailDto product)
    {
        return product.Attributes
            .OrderBy(attribute => attribute.AttributeKey, StringComparer.OrdinalIgnoreCase)
            .Select(attribute =>
            {
                var canonicalValue = FormatValue(attribute.Value, attribute.Unit);
                var evidenceItems = GetClaims(product, attribute)
                    .OrderByDescending(claim => claim.ObservedUtc)
                    .ThenByDescending(claim => claim.Confidence)
                    .ThenBy(claim => claim.SourceName, StringComparer.OrdinalIgnoreCase)
                    .Select(claim => new ProductEvidenceItemModel
                    {
                        SourceName = claim.SourceName,
                        SourceProductId = claim.SourceProductId,
                        SourceAttributeKey = claim.SourceAttributeKey,
                        SourceUrl = claim.SourceUrl,
                        SelectorOrPath = claim.SelectorOrPath,
                        DisplayValue = claim.DisplayValue,
                        Confidence = claim.Confidence,
                        ObservedUtc = claim.ObservedUtc,
                        MatchesCanonical = ValuesMatch(canonicalValue, claim.DisplayValue)
                    })
                    .ToArray();

                return new ProductEvidenceInspectorRowModel
                {
                    AttributeKey = attribute.AttributeKey,
                    CanonicalValue = canonicalValue,
                    HasConflict = HasDisagreement(attribute, product),
                    Evidence = evidenceItems
                };
            })
            .ToArray();
    }

    public static IReadOnlyList<ProductConflictPanelRowModel> GetConflictRows(ProductDetailDto product)
    {
        return product.Attributes
            .OrderBy(attribute => attribute.AttributeKey, StringComparer.OrdinalIgnoreCase)
            .Select(attribute => BuildConflictRow(product, attribute))
            .Where(row => row is not null)
            .Cast<ProductConflictPanelRowModel>()
            .ToArray();
    }

    public static IReadOnlyList<ProductHistoryTimelineEntryModel> GetHistoryTimeline(IEnumerable<ProductChangeEventDto> history)
    {
        return history
            .OrderByDescending(change => change.TimestampUtc)
            .Select(change => new ProductHistoryTimelineEntryModel
            {
                TimestampUtc = change.TimestampUtc,
                AttributeKey = change.AttributeKey,
                SourceName = change.SourceName,
                OldValue = FormatValue(change.OldValue),
                NewValue = FormatValue(change.NewValue),
                ChangeSummary = BuildChangeSummary(change)
            })
            .ToArray();
    }

    public static string FormatValue(object? value, string? unit = null)
    {
        var formatted = value switch
        {
            null => "-",
            string text when string.IsNullOrWhiteSpace(text) => "-",
            string text => text,
            JsonElement json => FormatJsonElement(json),
            bool boolean => boolean ? "True" : "False",
            DateTime timestamp => timestamp.ToString("u"),
            DateTimeOffset timestamp => timestamp.ToString("u"),
            IEnumerable<object?> sequence => string.Join(", ", sequence.Select(item => FormatValue(item))),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "-"
        };

        if (formatted == "-" || string.IsNullOrWhiteSpace(unit))
        {
            return formatted;
        }

        return $"{formatted} {unit}";
    }

    private static ProductSourceComparisonCellModel BuildSourceComparisonCell(ProductDetailDto product, ProductAttributeDetailDto attribute, ProductSourceComparisonColumnModel column)
    {
        var canonicalValue = FormatValue(attribute.Value, attribute.Unit);
        var claims = GetClaims(product, attribute)
            .Where(claim => string.Equals(claim.SourceProductId, column.SourceProductId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(claim.SourceName, column.SourceName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var claimValues = claims
            .Select(claim => claim.DisplayValue)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ProductSourceComparisonCellModel
        {
            SourceName = column.SourceName,
            Claims = claims.Select(claim => new ProductSourceClaimModel
            {
                DisplayValue = claim.DisplayValue,
                Confidence = claim.Confidence,
                SelectorOrPath = claim.SelectorOrPath,
                SourceAttributeKey = claim.SourceAttributeKey,
                ObservedUtc = claim.ObservedUtc,
                MatchesCanonical = ValuesMatch(canonicalValue, claim.DisplayValue)
            }).ToArray(),
            HasClaim = claims.Length > 0,
            MatchesCanonical = claimValues.Any(value => ValuesMatch(canonicalValue, value)),
            HasDisagreement = claimValues.Length > 1 || claimValues.Any(value => !ValuesMatch(canonicalValue, value))
        };
    }

    private static ProductConflictPanelRowModel? BuildConflictRow(ProductDetailDto product, ProductAttributeDetailDto attribute)
    {
        var canonicalValue = FormatValue(attribute.Value, attribute.Unit);
        var groups = GetClaims(product, attribute)
            .GroupBy(claim => NormalizeValue(claim.DisplayValue), StringComparer.OrdinalIgnoreCase)
            .Select(group => new ProductConflictValueGroupModel
            {
                DisplayValue = group.First().DisplayValue,
                IsCanonical = ValuesMatch(canonicalValue, group.First().DisplayValue),
                Sources = group
                    .Select(claim => claim.SourceName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            })
            .OrderByDescending(group => group.IsCanonical)
            .ThenByDescending(group => group.Sources.Count)
            .ThenBy(group => group.DisplayValue, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (!attribute.HasConflict && groups.Length <= 1)
        {
            return null;
        }

        return new ProductConflictPanelRowModel
        {
            AttributeKey = attribute.AttributeKey,
            CanonicalValue = canonicalValue,
            Groups = groups
        };
    }

    private static bool HasDisagreement(ProductAttributeDetailDto attribute, ProductDetailDto product)
    {
        if (attribute.HasConflict)
        {
            return true;
        }

        var canonicalValue = FormatValue(attribute.Value, attribute.Unit);
        var distinctClaims = GetClaims(product, attribute)
            .Select(claim => claim.DisplayValue)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return distinctClaims.Length > 1 || distinctClaims.Any(value => !ValuesMatch(canonicalValue, value));
    }

    private static IReadOnlyList<ProductAttributeClaim> GetClaims(ProductDetailDto product, ProductAttributeDetailDto attribute)
    {
        var evidenceClaims = attribute.Evidence
            .Select(evidence => new ProductAttributeClaim
            {
                SourceName = evidence.SourceName,
                SourceProductId = evidence.SourceProductId,
                SourceAttributeKey = evidence.SourceAttributeKey,
                DisplayValue = FormatValue(evidence.RawValue, attribute.Unit),
                SelectorOrPath = evidence.SelectorOrPath,
                SourceUrl = evidence.SourceUrl,
                Confidence = evidence.Confidence,
                ObservedUtc = evidence.ObservedUtc
            })
            .ToArray();

        if (evidenceClaims.Length > 0)
        {
            return evidenceClaims;
        }

        return product.SourceProducts
            .SelectMany(source => source.RawAttributes
                .Where(raw => string.Equals(raw.AttributeKey, attribute.AttributeKey, StringComparison.OrdinalIgnoreCase))
                .Select(raw => new ProductAttributeClaim
                {
                    SourceName = source.SourceName,
                    SourceProductId = source.Id,
                    SourceAttributeKey = raw.AttributeKey,
                    DisplayValue = FormatValue(raw.Value, raw.Unit),
                    SelectorOrPath = raw.SourcePath,
                    SourceUrl = source.SourceUrl,
                    Confidence = 0,
                    ObservedUtc = DateTime.MinValue
                }))
            .ToArray();
    }

    private static bool ValuesMatch(string left, string right)
    {
        return string.Equals(NormalizeValue(left), NormalizeValue(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeValue(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "-"
            : value.Trim();
    }

    private static string BuildChangeSummary(ProductChangeEventDto change)
    {
        var oldValue = FormatValue(change.OldValue);
        var newValue = FormatValue(change.NewValue);

        return (oldValue, newValue) switch
        {
            ("-", "-") => "No value change recorded",
            ("-", _) => $"Set to {newValue}",
            (_, "-") => $"Cleared from {oldValue}",
            _ => $"Changed from {oldValue} to {newValue}"
        };
    }

    private static string FormatJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Undefined => "-",
            JsonValueKind.Null => "-",
            JsonValueKind.String => element.GetString() ?? "-",
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.True => "True",
            JsonValueKind.False => "False",
            JsonValueKind.Array => string.Join(", ", element.EnumerateArray().Select(FormatJsonElement)),
            _ => element.ToString()
        };
    }

    private sealed class ProductAttributeClaim
    {
        public string SourceName { get; init; } = string.Empty;
        public string SourceProductId { get; init; } = string.Empty;
        public string SourceAttributeKey { get; init; } = string.Empty;
        public string DisplayValue { get; init; } = "-";
        public string? SelectorOrPath { get; init; }
        public string SourceUrl { get; init; } = string.Empty;
        public decimal Confidence { get; init; }
        public DateTime ObservedUtc { get; init; }
    }
}

public sealed class ProductSourceComparisonColumnModel
{
    public string SourceProductId { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? ModelNumber { get; init; }
    public string? Gtin { get; init; }
    public string SourceUrl { get; init; } = string.Empty;
}

public sealed class ProductSourceComparisonRowModel
{
    public string AttributeKey { get; init; } = string.Empty;
    public string CanonicalValue { get; init; } = "-";
    public bool HasConflict { get; init; }
    public int EvidenceCount { get; init; }
    public IReadOnlyList<ProductSourceComparisonCellModel> Cells { get; init; } = [];
}

public sealed class ProductSourceComparisonCellModel
{
    public string SourceName { get; init; } = string.Empty;
    public bool HasClaim { get; init; }
    public bool MatchesCanonical { get; init; }
    public bool HasDisagreement { get; init; }
    public IReadOnlyList<ProductSourceClaimModel> Claims { get; init; } = [];
}

public sealed class ProductSourceClaimModel
{
    public string DisplayValue { get; init; } = "-";
    public decimal Confidence { get; init; }
    public string SourceAttributeKey { get; init; } = string.Empty;
    public string? SelectorOrPath { get; init; }
    public DateTime ObservedUtc { get; init; }
    public bool MatchesCanonical { get; init; }
}

public sealed class ProductEvidenceInspectorRowModel
{
    public string AttributeKey { get; init; } = string.Empty;
    public string CanonicalValue { get; init; } = "-";
    public bool HasConflict { get; init; }
    public IReadOnlyList<ProductEvidenceItemModel> Evidence { get; init; } = [];
}

public sealed class ProductEvidenceItemModel
{
    public string SourceName { get; init; } = string.Empty;
    public string SourceProductId { get; init; } = string.Empty;
    public string SourceAttributeKey { get; init; } = string.Empty;
    public string SourceUrl { get; init; } = string.Empty;
    public string? SelectorOrPath { get; init; }
    public string DisplayValue { get; init; } = "-";
    public decimal Confidence { get; init; }
    public DateTime ObservedUtc { get; init; }
    public bool MatchesCanonical { get; init; }
}

public sealed class ProductConflictPanelRowModel
{
    public string AttributeKey { get; init; } = string.Empty;
    public string CanonicalValue { get; init; } = "-";
    public IReadOnlyList<ProductConflictValueGroupModel> Groups { get; init; } = [];
}

public sealed class ProductConflictValueGroupModel
{
    public string DisplayValue { get; init; } = "-";
    public bool IsCanonical { get; init; }
    public IReadOnlyList<string> Sources { get; init; } = [];
}

public sealed class ProductHistoryTimelineEntryModel
{
    public DateTime TimestampUtc { get; init; }
    public string AttributeKey { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public string OldValue { get; init; } = "-";
    public string NewValue { get; init; } = "-";
    public string ChangeSummary { get; init; } = string.Empty;
}