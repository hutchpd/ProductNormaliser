using System.Globalization;
using ProductNormaliser.AdminApi.Contracts;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Normalisation;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.AdminApi.Services;

public static class ProductAnalysisProjection
{
    public static ProductAnalysisSummary BuildSummary(CanonicalProduct product, ICategorySchemaRegistry schemaRegistry, ICategoryAttributeNormaliserRegistry attributeNormaliserRegistry)
    {
        var schema = schemaRegistry.GetSchema(product.CategoryKey);
        var schemaAttributes = schema?.Attributes ?? [];
        var schemaByKey = schemaAttributes.ToDictionary(attribute => attribute.Key, StringComparer.OrdinalIgnoreCase);

        var keyAttributeKeys = attributeNormaliserRegistry.GetCompletenessAttributeKeys(product.CategoryKey)
            .Concat(attributeNormaliserRegistry.GetIdentityAttributeKeys(product.CategoryKey))
            .Concat(schemaAttributes.Where(attribute => attribute.IsRequired).Select(attribute => attribute.Key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var populatedKeyAttributeCount = keyAttributeKeys.Count(key => TryGetMeaningfulAttribute(product, key, out _));
        var expectedKeyAttributeCount = keyAttributeKeys.Length == 0 ? Math.Max(1, product.Attributes.Count) : keyAttributeKeys.Length;
        var completenessScore = expectedKeyAttributeCount == 0
            ? 0m
            : decimal.Round((decimal)populatedKeyAttributeCount / expectedKeyAttributeCount, 2, MidpointRounding.AwayFromZero);

        var freshnessAgeDays = Math.Max(0, (int)Math.Floor((DateTime.UtcNow - product.UpdatedUtc).TotalDays));
        var evidenceCount = product.Attributes.Values.Sum(attribute => attribute.Evidence.Count);
        var conflictAttributeCount = product.Attributes.Values.Count(attribute => attribute.HasConflict);

        var keyAttributes = keyAttributeKeys
            .Select(key => TryBuildKeyAttribute(product, key, schemaByKey, out var attribute) ? attribute : null)
            .Where(attribute => attribute is not null)
            .Cast<ProductKeyAttributeDto>()
            .Take(6)
            .ToArray();

        if (keyAttributes.Length == 0)
        {
            keyAttributes = product.Attributes.Values
                .Where(attribute => HasMeaningfulValue(attribute.Value))
                .OrderByDescending(attribute => attribute.HasConflict)
                .ThenByDescending(attribute => attribute.Confidence)
                .ThenBy(attribute => attribute.AttributeKey, StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .Select(attribute => new ProductKeyAttributeDto
                {
                    AttributeKey = attribute.AttributeKey,
                    DisplayName = ToDisplayLabel(attribute.AttributeKey),
                    Value = FormatValue(attribute.Value, attribute.Unit),
                    HasConflict = attribute.HasConflict,
                    Confidence = attribute.Confidence
                })
                .ToArray();
        }

        return new ProductAnalysisSummary
        {
            SourceCount = product.Sources.Count,
            EvidenceCount = evidenceCount,
            ConflictAttributeCount = conflictAttributeCount,
            HasConflict = conflictAttributeCount > 0,
            CompletenessScore = completenessScore,
            CompletenessStatus = GetCompletenessStatus(completenessScore),
            PopulatedKeyAttributeCount = populatedKeyAttributeCount,
            ExpectedKeyAttributeCount = expectedKeyAttributeCount,
            FreshnessStatus = GetFreshnessStatus(freshnessAgeDays),
            FreshnessAgeDays = freshnessAgeDays,
            KeyAttributes = keyAttributes
        };
    }

    public static bool MatchesFilters(ProductAnalysisSummary summary, int? minSourceCount, string? freshness, string? conflictStatus, string? completenessStatus)
    {
        if (minSourceCount.HasValue && minSourceCount.Value > 0 && summary.SourceCount < minSourceCount.Value)
        {
            return false;
        }

        var normalizedFreshness = NormalizeFilter(freshness);
        if (!string.IsNullOrWhiteSpace(normalizedFreshness) && !string.Equals(normalizedFreshness, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(summary.FreshnessStatus, normalizedFreshness, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        var normalizedConflictStatus = NormalizeFilter(conflictStatus);
        if (normalizedConflictStatus is "with_conflicts" && !summary.HasConflict)
        {
            return false;
        }

        if (normalizedConflictStatus is "without_conflicts" && summary.HasConflict)
        {
            return false;
        }

        var normalizedCompleteness = NormalizeFilter(completenessStatus);
        if (!string.IsNullOrWhiteSpace(normalizedCompleteness) && !string.Equals(normalizedCompleteness, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(summary.CompletenessStatus, normalizedCompleteness, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public static string GetFreshnessStatus(int freshnessAgeDays)
    {
        return freshnessAgeDays switch
        {
            <= 7 => "fresh",
            <= 30 => "aging",
            _ => "stale"
        };
    }

    public static string GetCompletenessStatus(decimal completenessScore)
    {
        return completenessScore switch
        {
            >= 0.85m => "complete",
            >= 0.50m => "partial",
            _ => "sparse"
        };
    }

    public static string FormatValue(object? value, string? unit = null)
    {
        string formatted = value switch
        {
            null => "-",
            string text when string.IsNullOrWhiteSpace(text) => "-",
            string text => text,
            bool boolean => boolean ? "True" : "False",
            DateTime timestamp => timestamp.ToString("u"),
            DateTimeOffset timestamp => timestamp.ToString("u"),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "-"
        };

        if (formatted == "-" || string.IsNullOrWhiteSpace(unit))
        {
            return formatted;
        }

        return $"{formatted} {unit}";
    }

    private static bool TryBuildKeyAttribute(CanonicalProduct product, string attributeKey, IReadOnlyDictionary<string, CanonicalAttributeDefinition> schemaByKey, out ProductKeyAttributeDto attribute)
    {
        attribute = new ProductKeyAttributeDto();
        if (!TryGetMeaningfulAttribute(product, attributeKey, out var canonicalAttribute))
        {
            return false;
        }

        var displayName = schemaByKey.TryGetValue(attributeKey, out var definition)
            ? definition.DisplayName
            : ToDisplayLabel(attributeKey);

        attribute = new ProductKeyAttributeDto
        {
            AttributeKey = attributeKey,
            DisplayName = displayName,
            Value = FormatValue(canonicalAttribute.Value, canonicalAttribute.Unit),
            HasConflict = canonicalAttribute.HasConflict,
            Confidence = canonicalAttribute.Confidence
        };

        return true;
    }

    private static bool TryGetMeaningfulAttribute(CanonicalProduct product, string attributeKey, out CanonicalAttributeValue attribute)
    {
        if (product.Attributes.TryGetValue(attributeKey, out attribute!) && HasMeaningfulValue(attribute.Value))
        {
            return true;
        }

        attribute = default!;
        return false;
    }

    private static bool HasMeaningfulValue(object? value)
    {
        return value switch
        {
            null => false,
            string text => !string.IsNullOrWhiteSpace(text),
            _ => true
        };
    }

    private static string NormalizeFilter(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace('-', '_').ToLowerInvariant();
    }

    private static string ToDisplayLabel(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "Unknown";
        }

        return string.Join(' ', key.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));
    }
}

public sealed class ProductAnalysisSummary
{
    public int SourceCount { get; init; }
    public int EvidenceCount { get; init; }
    public int ConflictAttributeCount { get; init; }
    public bool HasConflict { get; init; }
    public decimal CompletenessScore { get; init; }
    public string CompletenessStatus { get; init; } = string.Empty;
    public int PopulatedKeyAttributeCount { get; init; }
    public int ExpectedKeyAttributeCount { get; init; }
    public string FreshnessStatus { get; init; } = string.Empty;
    public int FreshnessAgeDays { get; init; }
    public IReadOnlyList<ProductKeyAttributeDto> KeyAttributes { get; init; } = [];
}