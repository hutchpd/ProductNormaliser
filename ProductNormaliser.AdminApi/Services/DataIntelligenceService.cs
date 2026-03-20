using System.Globalization;
using MongoDB.Driver;
using ProductNormaliser.AdminApi.Contracts;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;
using ProductNormaliser.Infrastructure.Mongo;
using ProductNormaliser.Infrastructure.Mongo.Repositories;

namespace ProductNormaliser.AdminApi.Services;

public sealed class DataIntelligenceService(MongoDbContext mongoDbContext, IUnmappedAttributeStore unmappedAttributeStore) : IDataIntelligenceService
{
    private static readonly HashSet<string> DirectAttributeKeys = ["brand", "model_number", "gtin"];

    public async Task<DetailedCoverageResponse> GetDetailedCoverageAsync(string categoryKey, CancellationToken cancellationToken)
    {
        var schema = GetSchema(categoryKey);
        var canonicalProducts = await GetCanonicalProductsAsync(categoryKey, cancellationToken);
        var sourceProducts = await GetSourceProductsAsync(categoryKey, cancellationToken);

        var attributes = schema.Attributes
            .Select(definition => BuildCoverageDetail(definition, canonicalProducts))
            .OrderByDescending(attribute => attribute.MissingProductCount)
            .ThenBy(attribute => attribute.AttributeKey, StringComparer.Ordinal)
            .ToArray();

        return new DetailedCoverageResponse
        {
            CategoryKey = categoryKey,
            TotalCanonicalProducts = canonicalProducts.Count,
            TotalSourceProducts = sourceProducts.Count,
            Attributes = attributes,
            MostMissingAttributes = attributes
                .OrderByDescending(attribute => attribute.MissingProductCount)
                .ThenBy(attribute => attribute.AttributeKey, StringComparer.Ordinal)
                .Take(5)
                .Select(attribute => new AttributeGapDto
                {
                    AttributeKey = attribute.AttributeKey,
                    DisplayName = attribute.DisplayName,
                    ProductCount = attribute.MissingProductCount,
                    Percentage = attribute.CoveragePercent == 0m && canonicalProducts.Count == 0
                        ? 0m
                        : ToPercent(attribute.MissingProductCount, canonicalProducts.Count)
                })
                .ToArray(),
            MostConflictedAttributes = attributes
                .Where(attribute => attribute.ConflictProductCount > 0)
                .OrderByDescending(attribute => attribute.ConflictProductCount)
                .ThenBy(attribute => attribute.AttributeKey, StringComparer.Ordinal)
                .Take(5)
                .Select(attribute => new AttributeGapDto
                {
                    AttributeKey = attribute.AttributeKey,
                    DisplayName = attribute.DisplayName,
                    ProductCount = attribute.ConflictProductCount,
                    Percentage = ToPercent(attribute.ConflictProductCount, canonicalProducts.Count)
                })
                .ToArray()
        };
    }

    public async Task<IReadOnlyList<UnmappedAttributeDto>> GetUnmappedAttributesAsync(string categoryKey, CancellationToken cancellationToken)
    {
        var unmappedAttributes = await unmappedAttributeStore.ListAsync(categoryKey, cancellationToken);
        return unmappedAttributes
            .OrderByDescending(attribute => attribute.OccurrenceCount)
            .ThenBy(attribute => attribute.RawAttributeKey, StringComparer.OrdinalIgnoreCase)
            .Select(attribute => new UnmappedAttributeDto
            {
                CanonicalKey = attribute.CanonicalKey,
                RawAttributeKey = attribute.RawAttributeKey,
                OccurrenceCount = attribute.OccurrenceCount,
                SourceNames = attribute.SourceNames.OrderBy(source => source, StringComparer.OrdinalIgnoreCase).ToArray(),
                SampleValues = attribute.SampleValues.ToArray(),
                LastSeenUtc = attribute.LastSeenUtc
            })
            .ToArray();
    }

    public async Task<IReadOnlyList<SourceQualityScoreDto>> GetSourceQualityScoresAsync(string categoryKey, CancellationToken cancellationToken)
    {
        var schema = GetSchema(categoryKey);
        var schemaKeys = schema.Attributes.Select(attribute => attribute.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sourceProducts = await GetSourceProductsAsync(categoryKey, cancellationToken);
        var canonicalProducts = await GetCanonicalProductsAsync(categoryKey, cancellationToken);
        var sourceProductById = sourceProducts.ToDictionary(product => product.Id, StringComparer.OrdinalIgnoreCase);

        var scores = new List<SourceQualityScoreDto>();

        foreach (var sourceGroup in sourceProducts.GroupBy(product => product.SourceName, StringComparer.OrdinalIgnoreCase))
        {
            var groupProducts = sourceGroup.ToArray();
            var averageMappedAttributes = groupProducts.Length == 0
                ? 0m
                : decimal.Round(groupProducts.Average(product => (decimal)CountMappedAttributes(product, schemaKeys)), 2, MidpointRounding.AwayFromZero);
            var coveragePercent = schema.Attributes.Count == 0
                ? 0m
                : decimal.Round(averageMappedAttributes / schema.Attributes.Count * 100m, 2, MidpointRounding.AwayFromZero);

            var attributeConfidences = groupProducts
                .SelectMany(product => GetMappedConfidences(product, schemaKeys))
                .ToArray();
            var averageAttributeConfidence = attributeConfidences.Length == 0
                ? 0m
                : decimal.Round(attributeConfidences.Average() * 100m, 2, MidpointRounding.AwayFromZero);

            var (agreementCount, comparisonCount) = CountSourceAgreement(sourceGroup.Key, canonicalProducts, sourceProductById, schemaKeys);
            var agreementPercent = ToPercent(agreementCount, comparisonCount);
            var qualityScore = decimal.Round(
                coveragePercent * 0.50m
                + averageAttributeConfidence * 0.25m
                + agreementPercent * 0.25m,
                2,
                MidpointRounding.AwayFromZero);

            scores.Add(new SourceQualityScoreDto
            {
                SourceName = sourceGroup.Key,
                SourceProductCount = groupProducts.Length,
                AverageMappedAttributes = averageMappedAttributes,
                CoveragePercent = coveragePercent,
                AverageAttributeConfidence = averageAttributeConfidence,
                AgreementPercent = agreementPercent,
                QualityScore = qualityScore
            });
        }

        return scores
            .OrderByDescending(score => score.QualityScore)
            .ThenByDescending(score => score.SourceProductCount)
            .ThenBy(score => score.SourceName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static AttributeCoverageDetailDto BuildCoverageDetail(CanonicalAttributeDefinition definition, IReadOnlyList<CanonicalProduct> products)
    {
        var presentCount = 0;
        var conflictCount = 0;
        var agreementCount = 0;
        decimal confidenceTotal = 0m;

        foreach (var product in products)
        {
            if (!TryGetAttribute(product, definition.Key, out var value, out var confidence, out var hasConflict) || IsMissingValue(value))
            {
                continue;
            }

            presentCount += 1;
            confidenceTotal += confidence;

            if (hasConflict)
            {
                conflictCount += 1;
            }
            else
            {
                agreementCount += 1;
            }
        }

        var averageConfidence = presentCount == 0
            ? 0m
            : decimal.Round(confidenceTotal / presentCount, 2, MidpointRounding.AwayFromZero);
        var coveragePercent = ToPercent(presentCount, products.Count);
        var conflictPercent = ToPercent(conflictCount, products.Count);
        var agreementPercent = ToPercent(agreementCount, presentCount);

        return new AttributeCoverageDetailDto
        {
            AttributeKey = definition.Key,
            DisplayName = definition.DisplayName,
            PresentProductCount = presentCount,
            MissingProductCount = products.Count - presentCount,
            CoveragePercent = coveragePercent,
            ConflictProductCount = conflictCount,
            ConflictPercent = conflictPercent,
            AverageConfidence = averageConfidence,
            AgreementPercent = agreementPercent,
            ReliabilityScore = decimal.Round(averageConfidence * agreementPercent, 2, MidpointRounding.AwayFromZero)
        };
    }

    private static int CountMappedAttributes(SourceProduct product, IReadOnlySet<string> schemaKeys)
    {
        var count = 0;

        if (!string.IsNullOrWhiteSpace(product.Brand) && schemaKeys.Contains("brand"))
        {
            count += 1;
        }

        if (!string.IsNullOrWhiteSpace(product.ModelNumber) && schemaKeys.Contains("model_number"))
        {
            count += 1;
        }

        if (!string.IsNullOrWhiteSpace(product.Gtin) && schemaKeys.Contains("gtin"))
        {
            count += 1;
        }

        count += product.NormalisedAttributes.Values.Count(attribute => schemaKeys.Contains(attribute.AttributeKey) && !DirectAttributeKeys.Contains(attribute.AttributeKey));
        return count;
    }

    private static IEnumerable<decimal> GetMappedConfidences(SourceProduct product, IReadOnlySet<string> schemaKeys)
    {
        if (!string.IsNullOrWhiteSpace(product.Brand) && schemaKeys.Contains("brand"))
        {
            yield return 1.00m;
        }

        if (!string.IsNullOrWhiteSpace(product.ModelNumber) && schemaKeys.Contains("model_number"))
        {
            yield return 1.00m;
        }

        if (!string.IsNullOrWhiteSpace(product.Gtin) && schemaKeys.Contains("gtin"))
        {
            yield return 1.00m;
        }

        foreach (var attribute in product.NormalisedAttributes.Values.Where(attribute => schemaKeys.Contains(attribute.AttributeKey) && !DirectAttributeKeys.Contains(attribute.AttributeKey)))
        {
            yield return attribute.Confidence;
        }
    }

    private static (int AgreementCount, int ComparisonCount) CountSourceAgreement(
        string sourceName,
        IReadOnlyList<CanonicalProduct> canonicalProducts,
        IReadOnlyDictionary<string, SourceProduct> sourceProductsById,
        IReadOnlySet<string> schemaKeys)
    {
        var agreementCount = 0;
        var comparisonCount = 0;

        foreach (var canonicalProduct in canonicalProducts)
        {
            foreach (var sourceLink in canonicalProduct.Sources.Where(link => string.Equals(link.SourceName, sourceName, StringComparison.OrdinalIgnoreCase)))
            {
                if (!sourceProductsById.TryGetValue(sourceLink.SourceProductId, out var sourceProduct))
                {
                    continue;
                }

                foreach (var key in schemaKeys)
                {
                    if (!TryGetAttribute(canonicalProduct, key, out var canonicalValue, out _, out _) || IsMissingValue(canonicalValue))
                    {
                        continue;
                    }

                    if (!TryGetSourceValue(sourceProduct, key, out var sourceValue) || IsMissingValue(sourceValue))
                    {
                        continue;
                    }

                    comparisonCount += 1;
                    if (ValuesMatch(canonicalValue, sourceValue))
                    {
                        agreementCount += 1;
                    }
                }
            }
        }

        return (agreementCount, comparisonCount);
    }

    private static bool TryGetAttribute(CanonicalProduct product, string key, out object? value, out decimal confidence, out bool hasConflict)
    {
        switch (key)
        {
            case "brand":
                value = product.Brand;
                confidence = string.IsNullOrWhiteSpace(product.Brand) ? 0m : 1.00m;
                hasConflict = false;
                return true;
            case "model_number":
                value = product.ModelNumber;
                confidence = string.IsNullOrWhiteSpace(product.ModelNumber) ? 0m : 1.00m;
                hasConflict = false;
                return true;
            case "gtin":
                value = product.Gtin;
                confidence = string.IsNullOrWhiteSpace(product.Gtin) ? 0m : 1.00m;
                hasConflict = false;
                return true;
            default:
                if (product.Attributes.TryGetValue(key, out var attribute))
                {
                    value = attribute.Value;
                    confidence = attribute.Confidence;
                    hasConflict = attribute.HasConflict;
                    return true;
                }

                value = null;
                confidence = 0m;
                hasConflict = false;
                return false;
        }
    }

    private static bool TryGetSourceValue(SourceProduct product, string key, out object? value)
    {
        switch (key)
        {
            case "brand":
                value = product.Brand;
                return true;
            case "model_number":
                value = product.ModelNumber;
                return true;
            case "gtin":
                value = product.Gtin;
                return true;
            default:
                if (product.NormalisedAttributes.TryGetValue(key, out var attribute))
                {
                    value = attribute.Value;
                    return true;
                }

                value = null;
                return false;
        }
    }

    private static bool ValuesMatch(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        if (TryConvertToDecimal(left, out var leftDecimal) && TryConvertToDecimal(right, out var rightDecimal))
        {
            return leftDecimal == rightDecimal;
        }

        if (left is bool leftBool && right is bool rightBool)
        {
            return leftBool == rightBool;
        }

        return string.Equals(
            Convert.ToString(left, CultureInfo.InvariantCulture)?.Trim(),
            Convert.ToString(right, CultureInfo.InvariantCulture)?.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryConvertToDecimal(object value, out decimal converted)
    {
        switch (value)
        {
            case decimal decimalValue:
                converted = decimalValue;
                return true;
            case int intValue:
                converted = intValue;
                return true;
            case long longValue:
                converted = longValue;
                return true;
            case double doubleValue:
                converted = (decimal)doubleValue;
                return true;
            case float floatValue:
                converted = (decimal)floatValue;
                return true;
            case string stringValue when decimal.TryParse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed):
                converted = parsed;
                return true;
            default:
                converted = 0m;
                return false;
        }
    }

    private static bool IsMissingValue(object? value)
    {
        return value switch
        {
            null => true,
            string stringValue => string.IsNullOrWhiteSpace(stringValue),
            _ => false
        };
    }

    private static decimal ToPercent(int numerator, int denominator)
    {
        if (denominator == 0)
        {
            return 0m;
        }

        return decimal.Round((decimal)numerator / denominator * 100m, 2, MidpointRounding.AwayFromZero);
    }

    private async Task<List<CanonicalProduct>> GetCanonicalProductsAsync(string categoryKey, CancellationToken cancellationToken)
    {
        var cursor = await mongoDbContext.CanonicalProducts.FindAsync(
            product => product.CategoryKey == categoryKey,
            cancellationToken: cancellationToken);

        return await cursor.ToListAsync(cancellationToken);
    }

    private async Task<List<SourceProduct>> GetSourceProductsAsync(string categoryKey, CancellationToken cancellationToken)
    {
        var cursor = await mongoDbContext.SourceProducts.FindAsync(
            product => product.CategoryKey == categoryKey,
            cancellationToken: cancellationToken);

        return await cursor.ToListAsync(cancellationToken);
    }

    private static CategorySchema GetSchema(string categoryKey)
    {
        if (string.Equals(categoryKey, TvCategorySchemaProvider.CategoryKey, StringComparison.OrdinalIgnoreCase))
        {
            return new TvCategorySchemaProvider().GetSchema();
        }

        return new CategorySchema
        {
            CategoryKey = categoryKey,
            DisplayName = categoryKey,
            Attributes = []
        };
    }
}