using System.Globalization;
using MongoDB.Driver;
using ProductNormaliser.AdminApi.Contracts;
using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Normalisation;
using ProductNormaliser.Core.Schemas;
using ProductNormaliser.Infrastructure.Crawling;
using ProductNormaliser.Infrastructure.Mongo;
using ProductNormaliser.Infrastructure.Mongo.Repositories;

namespace ProductNormaliser.AdminApi.Services;

public sealed class DataIntelligenceService(
    MongoDbContext mongoDbContext,
    IUnmappedAttributeStore unmappedAttributeStore,
    ICrawlPriorityService? crawlPriorityService = null,
    ISourceTrustService? sourceTrustService = null,
    IAttributeStabilityService? attributeStabilityService = null,
    ISourceDisagreementService? sourceDisagreementService = null,
    ICategorySchemaRegistry? categorySchemaRegistry = null,
    ICategoryAttributeNormaliserRegistry? categoryAttributeNormaliserRegistry = null) : IDataIntelligenceService
{
    private static readonly HashSet<string> DirectAttributeKeys = ["brand", "model_number", "gtin"];
    private readonly ICategorySchemaRegistry categorySchemaRegistry = categorySchemaRegistry ?? DefaultCategoryRegistries.CreateSchemaRegistry();
    private readonly ICategoryAttributeNormaliserRegistry categoryAttributeNormaliserRegistry = categoryAttributeNormaliserRegistry ?? DefaultCategoryRegistries.CreateAttributeNormaliserRegistry();

    public async Task<DetailedCoverageResponse> GetDetailedCoverageAsync(string categoryKey, CancellationToken cancellationToken)
    {
        var schema = ResolveSchema(categoryKey);
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
        var schema = ResolveSchema(categoryKey);
        var evaluationKeys = GetEvaluationKeys(categoryKey, schema);
        var sourceProducts = await GetSourceProductsAsync(categoryKey, cancellationToken);
        var canonicalProducts = await GetCanonicalProductsAsync(categoryKey, cancellationToken);
        var sourceProductById = sourceProducts.ToDictionary(product => product.Id, StringComparer.OrdinalIgnoreCase);

        var scores = new List<SourceQualityScoreDto>();

        foreach (var sourceGroup in sourceProducts.GroupBy(product => product.SourceName, StringComparer.OrdinalIgnoreCase))
        {
            var groupProducts = sourceGroup.ToArray();
            var averageMappedAttributes = groupProducts.Length == 0
                ? 0m
                : decimal.Round(groupProducts.Average(product => (decimal)CountMappedAttributes(product, evaluationKeys)), 2, MidpointRounding.AwayFromZero);
            var coveragePercent = evaluationKeys.Count == 0
                ? 0m
                : decimal.Round(averageMappedAttributes / evaluationKeys.Count * 100m, 2, MidpointRounding.AwayFromZero);

            var attributeConfidences = groupProducts
                .SelectMany(product => GetMappedConfidences(product, evaluationKeys))
                .ToArray();
            var averageAttributeConfidence = attributeConfidences.Length == 0
                ? 0m
                : decimal.Round(attributeConfidences.Average() * 100m, 2, MidpointRounding.AwayFromZero);

            var (agreementCount, comparisonCount) = CountSourceAgreement(sourceGroup.Key, canonicalProducts, sourceProductById, evaluationKeys);
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

    public async Task<MergeInsightsResponse> GetMergeInsightsAsync(string categoryKey, CancellationToken cancellationToken)
    {
        var canonicalProducts = await GetCanonicalProductsAsync(categoryKey, cancellationToken);
        var canonicalIds = canonicalProducts.Select(product => product.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var conflicts = await mongoDbContext.MergeConflicts.Find(Builders<MergeConflict>.Filter.Empty).ToListAsync(cancellationToken);
        var openConflicts = conflicts
            .Where(conflict => string.Equals(conflict.Status, "open", StringComparison.OrdinalIgnoreCase)
                && canonicalIds.Contains(conflict.CanonicalProductId))
            .OrderByDescending(conflict => conflict.Severity)
            .ThenByDescending(conflict => conflict.CreatedUtc)
            .Select(conflict => new MergeConflictInsightDto
            {
                Id = conflict.Id,
                CanonicalProductId = conflict.CanonicalProductId,
                AttributeKey = conflict.AttributeKey,
                CurrentValue = conflict.ExistingValue,
                IncomingValue = conflict.IncomingValue,
                Reason = conflict.Reason,
                Severity = conflict.Severity,
                SuggestedValue = conflict.SuggestedValue,
                SuggestedSourceName = conflict.SuggestedSourceName,
                SuggestedConfidence = conflict.SuggestedConfidence,
                HighestConfidenceValue = conflict.HighestConfidenceValue,
                CreatedUtc = conflict.CreatedUtc
            })
            .ToArray();

        var suggestions = (await unmappedAttributeStore.ListAsync(categoryKey, cancellationToken))
            .Select(unmapped => BuildSuggestion(unmapped, ResolveSchema(categoryKey)))
            .Where(suggestion => suggestion is not null)
            .Select(suggestion => suggestion!)
            .OrderByDescending(suggestion => suggestion.Confidence)
            .ThenByDescending(suggestion => suggestion.OccurrenceCount)
            .Take(10)
            .ToArray();

        return new MergeInsightsResponse
        {
            CategoryKey = categoryKey,
            OpenConflicts = openConflicts,
            AttributeSuggestions = suggestions
        };
    }

    public Task<IReadOnlyList<SourceQualitySnapshotDto>> GetSourceHistoryAsync(string categoryKey, string? sourceName, int? timeRangeDays, CancellationToken cancellationToken)
    {
        var history = sourceTrustService?.GetSourceHistory(categoryKey, sourceName, timeRangeDays) ?? [];
        return Task.FromResult<IReadOnlyList<SourceQualitySnapshotDto>>(history
            .Select(snapshot => new SourceQualitySnapshotDto
            {
                SourceName = snapshot.SourceName,
                CategoryKey = snapshot.CategoryKey,
                TimestampUtc = snapshot.TimestampUtc,
                AttributeCoverage = snapshot.AttributeCoverage,
                ConflictRate = snapshot.ConflictRate,
                AgreementRate = snapshot.AgreementRate,
                SuccessfulCrawlRate = snapshot.SuccessfulCrawlRate,
                PriceVolatilityScore = snapshot.PriceVolatilityScore,
                SpecStabilityScore = snapshot.SpecStabilityScore,
                HistoricalTrustScore = snapshot.HistoricalTrustScore
            })
            .ToArray());
    }

    public Task<IReadOnlyList<AttributeStabilityDto>> GetAttributeStabilityAsync(string categoryKey, CancellationToken cancellationToken)
    {
        var scores = attributeStabilityService?.GetScores(categoryKey) ?? [];
        return Task.FromResult<IReadOnlyList<AttributeStabilityDto>>(scores
            .Select(score => new AttributeStabilityDto
            {
                CategoryKey = score.CategoryKey,
                AttributeKey = score.AttributeKey,
                ChangeCount = score.ChangeCount,
                OscillationCount = score.OscillationCount,
                DistinctValueCount = score.DistinctValueCount,
                StabilityScore = score.StabilityScore,
                IsSuspicious = score.IsSuspicious,
                SuspicionReason = score.SuspicionReason
            })
            .ToArray());
    }

    public Task<IReadOnlyList<SourceAttributeDisagreementDto>> GetSourceDisagreementsAsync(string categoryKey, string? sourceName, int? timeRangeDays, CancellationToken cancellationToken)
    {
        var disagreements = sourceDisagreementService?.GetDisagreements(categoryKey, sourceName, timeRangeDays) ?? [];
        return Task.FromResult<IReadOnlyList<SourceAttributeDisagreementDto>>(disagreements
            .Select(item => new SourceAttributeDisagreementDto
            {
                SourceName = item.SourceName,
                CategoryKey = item.CategoryKey,
                AttributeKey = item.AttributeKey,
                TotalComparisons = item.TotalComparisons,
                TimesDisagreed = item.TimesDisagreed,
                TimesWon = item.TimesWon,
                DisagreementRate = item.DisagreementRate,
                WinRate = item.WinRate,
                LastUpdatedUtc = item.LastUpdatedUtc
            })
            .ToArray());
    }

    public async Task<IReadOnlyList<QueuePriorityDto>> GetQueuePrioritiesAsync(CancellationToken cancellationToken)
    {
        if (crawlPriorityService is null)
        {
            return [];
        }

        var priorities = await crawlPriorityService.GetPrioritiesAsync(DateTime.UtcNow, cancellationToken);
        return priorities.Select(priority => new QueuePriorityDto
        {
            Id = priority.QueueItem.Id,
            SourceName = priority.QueueItem.SourceName,
            SourceUrl = priority.QueueItem.SourceUrl,
            CategoryKey = priority.QueueItem.CategoryKey,
            PriorityScore = priority.PriorityScore,
            SourceQualityScore = priority.SourceQualityScore,
            ChangeFrequencyScore = priority.ChangeFrequencyScore,
            PriceVolatilityScore = priority.PriceVolatilityScore,
            SpecStabilityScore = priority.SpecStabilityScore,
            MissingAttributeScore = priority.MissingAttributeScore,
            StalenessScore = priority.StalenessScore,
            MissingAttributeCount = priority.MissingAttributeCount,
            NextAttemptUtc = priority.QueueItem.NextAttemptUtc,
            EnqueuedUtc = priority.QueueItem.EnqueuedUtc,
            LastCrawledUtc = priority.LastCrawledUtc,
            Reasons = priority.Reasons
        }).ToArray();
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

    private HashSet<string> GetEvaluationKeys(string categoryKey, CategorySchema schema)
    {
        var keys = schema.Attributes
            .Where(attribute => attribute.IsRequired)
            .Select(attribute => attribute.Key)
            .Concat(categoryAttributeNormaliserRegistry.GetCompletenessAttributeKeys(categoryKey))
            .Concat(categoryAttributeNormaliserRegistry.GetIdentityAttributeKeys(categoryKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (keys.Count > 0)
        {
            return keys;
        }

        return schema.Attributes
            .Select(attribute => attribute.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private CategorySchema ResolveSchema(string categoryKey)
    {
        return categorySchemaRegistry.GetSchema(categoryKey)
            ?? new CategorySchema
            {
                CategoryKey = categoryKey,
                DisplayName = categoryKey,
                Attributes = []
            };
    }

    private static AttributeMappingSuggestionDto? BuildSuggestion(UnmappedAttribute unmappedAttribute, CategorySchema schema)
    {
        if (schema.Attributes.Count == 0)
        {
            return null;
        }

        var bestMatch = schema.Attributes
            .Select(attribute => new
            {
                attribute.Key,
                Score = ScoreSuggestion(unmappedAttribute.RawAttributeKey, attribute)
            })
            .OrderByDescending(candidate => candidate.Score)
            .First();

        if (bestMatch.Score < 0.60m)
        {
            return null;
        }

        var persistenceWindowDays = Math.Max(1m, (decimal)(unmappedAttribute.LastSeenUtc - unmappedAttribute.FirstSeenUtc).TotalDays + 1m);
        var persistenceBoost = Math.Min(0.15m, persistenceWindowDays / 30m * 0.15m);
        var occurrenceBoost = Math.Min(0.10m, unmappedAttribute.OccurrenceCount / 20m * 0.10m);

        return new AttributeMappingSuggestionDto
        {
            RawAttributeKey = unmappedAttribute.RawAttributeKey,
            SuggestedCanonicalKey = bestMatch.Key,
            Confidence = Math.Min(0.99m, decimal.Round(bestMatch.Score + persistenceBoost + occurrenceBoost, 2, MidpointRounding.AwayFromZero)),
            OccurrenceCount = unmappedAttribute.OccurrenceCount,
            SourceNames = unmappedAttribute.SourceNames.OrderBy(source => source, StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private static decimal ScoreSuggestion(string rawAttributeKey, CanonicalAttributeDefinition definition)
    {
        var rawTokens = Tokenise(rawAttributeKey);
        var candidateTokens = Tokenise($"{definition.Key} {definition.DisplayName}");
        if (rawTokens.Count == 0 || candidateTokens.Count == 0)
        {
            return 0m;
        }

        var overlap = rawTokens.Intersect(candidateTokens, StringComparer.OrdinalIgnoreCase).Count();
        var union = rawTokens.Union(candidateTokens, StringComparer.OrdinalIgnoreCase).Count();
        var jaccard = union == 0 ? 0m : decimal.Divide(overlap, union);
        var containsBoost = rawTokens.All(token => candidateTokens.Contains(token, StringComparer.OrdinalIgnoreCase)) ? 0.20m : 0m;
        var unitBoost = rawTokens.Contains("inch", StringComparer.OrdinalIgnoreCase) && string.Equals(definition.Unit, "inch", StringComparison.OrdinalIgnoreCase) ? 0.20m : 0m;
        var screenBoost = rawTokens.Contains("screen", StringComparer.OrdinalIgnoreCase) && candidateTokens.Contains("screen", StringComparer.OrdinalIgnoreCase) ? 0.15m : 0m;

        return Math.Min(0.99m, decimal.Round(jaccard + containsBoost + unitBoost + screenBoost, 2, MidpointRounding.AwayFromZero));
    }

    private static HashSet<string> Tokenise(string value)
    {
        return value
            .Replace("_", " ", StringComparison.Ordinal)
            .Replace("-", " ", StringComparison.Ordinal)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}