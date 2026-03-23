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

    public static ProductInvestigationSummaryModel GetInvestigationSummary(ProductDetailDto product, IEnumerable<ProductChangeEventDto> history)
    {
        var timeline = GetHistoryTimeline(product, history);
        var driftIndicators = GetSourceDriftIndicators(product, history);

        return new ProductInvestigationSummaryModel
        {
            TotalChanges = timeline.Count,
            ChangedAttributeCount = timeline
                .Select(change => change.AttributeKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            InvolvedSourceCount = timeline
                .Select(change => change.SourceName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            DriftingSourceCount = driftIndicators.Count(indicator => indicator.DriftBadge.Tone is "warning" or "danger"),
            LatestChange = timeline.FirstOrDefault()
        };
    }

    public static IReadOnlyList<ProductHistoryTimelineEntryModel> GetHistoryTimeline(ProductDetailDto product, IEnumerable<ProductChangeEventDto> history)
    {
        return history
            .OrderByDescending(change => change.TimestampUtc)
            .Select(change => BuildTimelineEntry(product, change))
            .ToArray();
    }

    public static IReadOnlyList<ProductAttributeHistoryGroupModel> GetAttributeHistory(ProductDetailDto product, IEnumerable<ProductChangeEventDto> history)
    {
        return history
            .GroupBy(change => change.AttributeKey, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Max(change => change.TimestampUtc))
            .Select(group => BuildAttributeHistoryGroup(product, group.Key, group))
            .ToArray();
    }

    public static IReadOnlyList<ProductSourceDriftIndicatorModel> GetSourceDriftIndicators(ProductDetailDto product, IEnumerable<ProductChangeEventDto> history)
    {
        var historyBySource = history
            .GroupBy(change => change.SourceName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(change => change.TimestampUtc).ToArray(), StringComparer.OrdinalIgnoreCase);

        return product.SourceProducts
            .Select(sourceProduct => sourceProduct.SourceName)
            .Concat(historyBySource.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(sourceName => sourceName, StringComparer.OrdinalIgnoreCase)
            .Select(sourceName => BuildSourceDriftIndicator(product, sourceName, historyBySource.GetValueOrDefault(sourceName) ?? []))
            .ToArray();
    }

    public static IReadOnlyList<ProductCanonicalExplanationModel> GetCanonicalExplanations(ProductDetailDto product, IEnumerable<ProductChangeEventDto> history)
    {
        return product.Attributes
            .OrderByDescending(attribute => attribute.HasConflict)
            .ThenBy(attribute => attribute.AttributeKey, StringComparer.OrdinalIgnoreCase)
            .Select(attribute => BuildCanonicalExplanation(product, attribute, history))
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

    private static ProductHistoryTimelineEntryModel BuildTimelineEntry(ProductDetailDto product, ProductChangeEventDto change)
    {
        var currentCanonicalValue = GetCurrentCanonicalValue(product, change.AttributeKey);
        var sourceAlignment = GetSourceAlignment(product, change.AttributeKey, change.SourceName);

        return new ProductHistoryTimelineEntryModel
        {
            TimestampUtc = change.TimestampUtc,
            AttributeKey = change.AttributeKey,
            SourceName = change.SourceName,
            OldValue = FormatValue(change.OldValue),
            NewValue = FormatValue(change.NewValue),
            ChangeSummary = BuildChangeSummary(change),
            CurrentCanonicalValue = currentCanonicalValue,
            SourceAlignmentBadge = sourceAlignment.Badge,
            SourceAlignmentSummary = sourceAlignment.Summary
        };
    }

    private static ProductAttributeHistoryGroupModel BuildAttributeHistoryGroup(ProductDetailDto product, string attributeKey, IEnumerable<ProductChangeEventDto> history)
    {
        var events = history
            .OrderByDescending(change => change.TimestampUtc)
            .Select(change => BuildTimelineEntry(product, change))
            .ToArray();
        var distinctSources = events
            .Select(change => change.SourceName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var distinctValues = history
            .SelectMany(change => new[] { FormatValue(change.OldValue), FormatValue(change.NewValue) })
            .Where(value => !string.Equals(value, "-", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var hasConflictingHistory = distinctSources.Length > 1 && distinctValues.Length > 1;
        var latestChange = events[0];

        return new ProductAttributeHistoryGroupModel
        {
            AttributeKey = attributeKey,
            CurrentCanonicalValue = GetCurrentCanonicalValue(product, attributeKey),
            HistoryBadge = hasConflictingHistory
                ? new StatusBadgeModel { Text = "Conflicting history", Tone = "warning" }
                : events.Length > 1
                    ? new StatusBadgeModel { Text = "Repeated changes", Tone = "neutral" }
                    : new StatusBadgeModel { Text = "Single change", Tone = "completed" },
            Summary = hasConflictingHistory
                ? $"{events.Length} canonical changes were driven by {distinctSources.Length} sources across {distinctValues.Length} recorded values."
                : events.Length == 1
                    ? $"One recorded canonical change, most recently set by {latestChange.SourceName}."
                    : $"{events.Length} recorded canonical changes, most recently set by {latestChange.SourceName}.",
            LatestChangeSummary = $"Latest change: {latestChange.TimestampUtc:u} by {latestChange.SourceName}.",
            Events = events,
            HasConflictingHistory = hasConflictingHistory
        };
    }

    private static ProductSourceDriftIndicatorModel BuildSourceDriftIndicator(ProductDetailDto product, string sourceName, IReadOnlyList<ProductChangeEventDto> sourceHistory)
    {
        var attributeAlignments = product.Attributes
            .Select(attribute => GetSourceAlignment(product, attribute.AttributeKey, sourceName))
            .Where(alignment => alignment.HasClaim)
            .ToArray();
        var divergingAttributes = attributeAlignments
            .Where(alignment => alignment.Badge.Tone is "danger" or "warning")
            .Select(alignment => alignment.AttributeKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(attributeKey => attributeKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var distinctAttributesChanged = sourceHistory
            .Select(change => change.AttributeKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var driftBadge = divergingAttributes.Length >= 2 || sourceHistory.Count >= 3
            ? new StatusBadgeModel { Text = "Drifting", Tone = "danger" }
            : divergingAttributes.Length == 1 || sourceHistory.Count >= 2
                ? new StatusBadgeModel { Text = "Watch", Tone = "warning" }
                : new StatusBadgeModel { Text = "Stable", Tone = "completed" };

        var summary = sourceHistory.Count == 0 && divergingAttributes.Length == 0
            ? "No recorded canonical changes and current claims align with the canonical record."
            : $"{sourceHistory.Count} recorded change event{(sourceHistory.Count == 1 ? string.Empty : "s")} across {distinctAttributesChanged} attribute{(distinctAttributesChanged == 1 ? string.Empty : "s")}; currently diverges on {divergingAttributes.Length} attribute{(divergingAttributes.Length == 1 ? string.Empty : "s")}.";

        return new ProductSourceDriftIndicatorModel
        {
            SourceName = sourceName,
            DriftBadge = driftBadge,
            ChangeCount = sourceHistory.Count,
            ChangedAttributeCount = distinctAttributesChanged,
            DivergingAttributeCount = divergingAttributes.Length,
            DivergingAttributes = divergingAttributes,
            LastChangedUtc = sourceHistory.FirstOrDefault()?.TimestampUtc,
            Summary = summary
        };
    }

    private static ProductCanonicalExplanationModel BuildCanonicalExplanation(ProductDetailDto product, ProductAttributeDetailDto attribute, IEnumerable<ProductChangeEventDto> history)
    {
        var canonicalValue = FormatValue(attribute.Value, attribute.Unit);
        var claims = GetClaims(product, attribute)
            .OrderByDescending(claim => claim.Confidence)
            .ThenByDescending(claim => claim.ObservedUtc)
            .ToArray();
        var supportingClaims = claims
            .Where(claim => ValuesMatch(canonicalValue, claim.DisplayValue))
            .ToArray();
        var opposingClaims = claims
            .Where(claim => !ValuesMatch(canonicalValue, claim.DisplayValue))
            .ToArray();
        var latestChange = history
            .Where(change => string.Equals(change.AttributeKey, attribute.AttributeKey, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(change => change.TimestampUtc)
            .FirstOrDefault();
        var highestConfidenceSupport = supportingClaims.FirstOrDefault();

        var badge = supportingClaims.Length == 0
            ? new StatusBadgeModel { Text = "Merged state", Tone = "neutral" }
            : opposingClaims.Length == 0
                ? new StatusBadgeModel { Text = "Aligned", Tone = "completed" }
                : new StatusBadgeModel { Text = "Contested", Tone = "warning" };

        var whyCurrentSummary = supportingClaims.Length == 0
            ? "No direct supporting evidence claim is attached in the current payload, so this value is being carried forward from the merged canonical state."
            : opposingClaims.Length == 0
                ? $"All {supportingClaims.Length} current evidence claim{(supportingClaims.Length == 1 ? string.Empty : "s")} align with the canonical value."
                : $"The canonical value remains in place because {supportingClaims.Length} current supporting claim{(supportingClaims.Length == 1 ? string.Empty : "s")} still back it, while {opposingClaims.Length} claim{(opposingClaims.Length == 1 ? string.Empty : "s")} disagree.";

        var strongestSupportSummary = highestConfidenceSupport is null
            ? "No current supporting evidence claim is attached to this attribute."
            : $"Strongest current support comes from {highestConfidenceSupport.SourceName} at {highestConfidenceSupport.Confidence:0.##} confidence with value {highestConfidenceSupport.DisplayValue}.";

        var lastChangedSummary = latestChange is null
            ? "No canonical change event is recorded for this attribute."
            : $"Last canonical change: {latestChange.TimestampUtc:u} by {latestChange.SourceName}. {BuildChangeSummary(latestChange)}.";

        return new ProductCanonicalExplanationModel
        {
            AttributeKey = attribute.AttributeKey,
            CanonicalValue = canonicalValue,
            DecisionBadge = badge,
            WhyCurrentSummary = whyCurrentSummary,
            StrongestSupportSummary = strongestSupportSummary,
            LastChangedSummary = lastChangedSummary,
            SupportingSources = supportingClaims.Select(claim => claim.SourceName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
            OpposingSources = opposingClaims.Select(claim => claim.SourceName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray()
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

    private static string GetCurrentCanonicalValue(ProductDetailDto product, string attributeKey)
    {
        var attribute = product.Attributes.FirstOrDefault(candidate => string.Equals(candidate.AttributeKey, attributeKey, StringComparison.OrdinalIgnoreCase));
        return attribute is null ? "-" : FormatValue(attribute.Value, attribute.Unit);
    }

    private static SourceAlignmentModel GetSourceAlignment(ProductDetailDto product, string attributeKey, string sourceName)
    {
        var attribute = product.Attributes.FirstOrDefault(candidate => string.Equals(candidate.AttributeKey, attributeKey, StringComparison.OrdinalIgnoreCase));
        if (attribute is null)
        {
            return new SourceAlignmentModel
            {
                AttributeKey = attributeKey,
                HasClaim = false,
                Badge = new StatusBadgeModel { Text = "No current claim", Tone = "neutral" },
                Summary = "No current canonical attribute is available for comparison."
            };
        }

        var canonicalValue = FormatValue(attribute.Value, attribute.Unit);
        var claims = GetClaims(product, attribute)
            .Where(claim => string.Equals(claim.SourceName, sourceName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (claims.Length == 0)
        {
            return new SourceAlignmentModel
            {
                AttributeKey = attributeKey,
                HasClaim = false,
                Badge = new StatusBadgeModel { Text = "No current claim", Tone = "neutral" },
                Summary = $"{sourceName} does not currently supply a claim for this attribute."
            };
        }

        var distinctValues = claims
            .Select(claim => claim.DisplayValue)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var matchesCanonical = distinctValues.Any(value => ValuesMatch(canonicalValue, value));
        var onlyCanonical = distinctValues.All(value => ValuesMatch(canonicalValue, value));

        if (matchesCanonical && onlyCanonical)
        {
            return new SourceAlignmentModel
            {
                AttributeKey = attributeKey,
                HasClaim = true,
                Badge = new StatusBadgeModel { Text = "Still aligns", Tone = "completed" },
                Summary = $"{sourceName} still supports the current canonical value of {canonicalValue}."
            };
        }

        if (matchesCanonical)
        {
            return new SourceAlignmentModel
            {
                AttributeKey = attributeKey,
                HasClaim = true,
                Badge = new StatusBadgeModel { Text = "Mixed", Tone = "warning" },
                Summary = $"{sourceName} currently provides mixed claims; at least one still matches {canonicalValue}."
            };
        }

        return new SourceAlignmentModel
        {
            AttributeKey = attributeKey,
            HasClaim = true,
            Badge = new StatusBadgeModel { Text = "Currently diverges", Tone = "danger" },
            Summary = $"{sourceName} currently disagrees with the canonical value of {canonicalValue}."
        };
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
    public string CurrentCanonicalValue { get; init; } = "-";
    public StatusBadgeModel SourceAlignmentBadge { get; init; } = new() { Text = "Unknown", Tone = "neutral" };
    public string SourceAlignmentSummary { get; init; } = string.Empty;
}

public sealed class ProductInvestigationSummaryModel
{
    public int TotalChanges { get; init; }
    public int ChangedAttributeCount { get; init; }
    public int InvolvedSourceCount { get; init; }
    public int DriftingSourceCount { get; init; }
    public ProductHistoryTimelineEntryModel? LatestChange { get; init; }
}

public sealed class ProductAttributeHistoryGroupModel
{
    public string AttributeKey { get; init; } = string.Empty;
    public string CurrentCanonicalValue { get; init; } = "-";
    public StatusBadgeModel HistoryBadge { get; init; } = new() { Text = "Unknown", Tone = "neutral" };
    public string Summary { get; init; } = string.Empty;
    public string LatestChangeSummary { get; init; } = string.Empty;
    public IReadOnlyList<ProductHistoryTimelineEntryModel> Events { get; init; } = [];
    public bool HasConflictingHistory { get; init; }
}

public sealed class ProductSourceDriftIndicatorModel
{
    public string SourceName { get; init; } = string.Empty;
    public StatusBadgeModel DriftBadge { get; init; } = new() { Text = "Unknown", Tone = "neutral" };
    public int ChangeCount { get; init; }
    public int ChangedAttributeCount { get; init; }
    public int DivergingAttributeCount { get; init; }
    public IReadOnlyList<string> DivergingAttributes { get; init; } = [];
    public DateTime? LastChangedUtc { get; init; }
    public string Summary { get; init; } = string.Empty;
}

public sealed class ProductCanonicalExplanationModel
{
    public string AttributeKey { get; init; } = string.Empty;
    public string CanonicalValue { get; init; } = "-";
    public StatusBadgeModel DecisionBadge { get; init; } = new() { Text = "Unknown", Tone = "neutral" };
    public string WhyCurrentSummary { get; init; } = string.Empty;
    public string StrongestSupportSummary { get; init; } = string.Empty;
    public string LastChangedSummary { get; init; } = string.Empty;
    public IReadOnlyList<string> SupportingSources { get; init; } = [];
    public IReadOnlyList<string> OpposingSources { get; init; } = [];
}

internal sealed class SourceAlignmentModel
{
    public string AttributeKey { get; init; } = string.Empty;
    public bool HasClaim { get; init; }
    public StatusBadgeModel Badge { get; init; } = new() { Text = "Unknown", Tone = "neutral" };
    public string Summary { get; init; } = string.Empty;
}