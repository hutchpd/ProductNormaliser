using MongoDB.Driver;
using ProductNormaliser.AdminApi.Contracts;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Normalisation;
using ProductNormaliser.Core.Schemas;
using ProductNormaliser.Infrastructure.Mongo;
using ProductNormaliser.Infrastructure.Mongo.Repositories;

namespace ProductNormaliser.AdminApi.Services;

public sealed class AdminQueryService(
    ICrawlLogStore crawlLogStore,
    ICanonicalProductStore canonicalProductStore,
    ISourceProductStore sourceProductStore,
    IProductChangeEventStore productChangeEventStore,
    MongoDbContext mongoDbContext,
    ICategorySchemaRegistry? categorySchemaRegistry = null,
    ICategoryAttributeNormaliserRegistry? categoryAttributeNormaliserRegistry = null) : IAdminQueryService
{
    private readonly ICategorySchemaRegistry categorySchemaRegistry = categorySchemaRegistry ?? new CategorySchemaRegistry([new TvCategorySchemaProvider(), new MonitorCategorySchemaProvider(), new LaptopCategorySchemaProvider(), new RefrigeratorCategorySchemaProvider()]);
    private readonly ICategoryAttributeNormaliserRegistry categoryAttributeNormaliserRegistry = categoryAttributeNormaliserRegistry ?? new CategoryAttributeNormaliserRegistry([
        new TvAttributeNormaliser(),
        new MonitorAttributeNormaliser(),
        new LaptopAttributeNormaliser(),
        new RefrigeratorAttributeNormaliser()
    ]);

    public async Task<IReadOnlyList<CrawlLogDto>> GetCrawlLogsAsync(CancellationToken cancellationToken)
    {
        var logs = await crawlLogStore.ListAsync(cancellationToken: cancellationToken);
        return logs.Select(Map).ToArray();
    }

    public async Task<CrawlLogDto?> GetCrawlLogAsync(string id, CancellationToken cancellationToken)
    {
        var log = await crawlLogStore.GetByIdAsync(id, cancellationToken);
        return log is null ? null : Map(log);
    }

    public async Task<IReadOnlyList<QueueItemDto>> GetQueueAsync(CancellationToken cancellationToken)
    {
        var cursor = await mongoDbContext.CrawlQueueItems.FindAsync(Builders<CrawlQueueItem>.Filter.Empty, cancellationToken: cancellationToken);
        var queueItems = await cursor.ToListAsync(cancellationToken);
        return queueItems.OrderBy(item => item.EnqueuedUtc).Select(item => new QueueItemDto
        {
            Id = item.Id,
            SourceName = item.SourceName,
            SourceUrl = item.SourceUrl,
            CategoryKey = item.CategoryKey,
            Status = item.Status,
            AttemptCount = item.AttemptCount,
            ConsecutiveFailureCount = item.ConsecutiveFailureCount,
            ImportanceScore = item.ImportanceScore,
            EnqueuedUtc = item.EnqueuedUtc,
            LastAttemptUtc = item.LastAttemptUtc,
            NextAttemptUtc = item.NextAttemptUtc,
            LastError = item.LastError
        }).ToArray();
    }

    public async Task<ProductListResponse> ListProductsAsync(string? categoryKey, string? search, int? minSourceCount, string? freshness, string? conflictStatus, string? completenessStatus, string? sort, int page, int pageSize, CancellationToken cancellationToken)
    {
        var normalizedCategoryKey = string.IsNullOrWhiteSpace(categoryKey) ? null : categoryKey.Trim();
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var effectivePage = Math.Max(1, page);
        var effectivePageSize = Math.Clamp(pageSize, 1, 100);

        var filter = Builders<CanonicalProduct>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(normalizedCategoryKey))
        {
            filter &= Builders<CanonicalProduct>.Filter.Eq(product => product.CategoryKey, normalizedCategoryKey);
        }

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var regex = new MongoDB.Bson.BsonRegularExpression(normalizedSearch, "i");
            filter &= Builders<CanonicalProduct>.Filter.Or(
                Builders<CanonicalProduct>.Filter.Eq(product => product.Id, normalizedSearch),
                Builders<CanonicalProduct>.Filter.Regex(product => product.DisplayName, regex),
                Builders<CanonicalProduct>.Filter.Regex(product => product.Brand, regex),
                Builders<CanonicalProduct>.Filter.Regex(product => product.ModelNumber, regex),
                Builders<CanonicalProduct>.Filter.Regex(product => product.Gtin, regex));
        }

        var products = await mongoDbContext.CanonicalProducts.Find(filter)
            .SortByDescending(product => product.UpdatedUtc)
            .ToListAsync(cancellationToken);

        var summaries = products
            .Select(product => new
            {
                Product = product,
                Analysis = ProductAnalysisProjection.BuildSummary(product, categorySchemaRegistry, categoryAttributeNormaliserRegistry)
            })
            .Where(entry => ProductAnalysisProjection.MatchesFilters(entry.Analysis, minSourceCount, freshness, conflictStatus, completenessStatus))
            .Select(entry => (entry.Product, entry.Analysis))
            .ToArray();

        var sortedItems = ProductAnalysisProjection.ApplySort(summaries, sort);

        var totalCount = sortedItems.Count;
        var pagedItems = sortedItems
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToArray();

        return new ProductListResponse
        {
            Items = pagedItems.Select(entry => new ProductSummaryResponse
            {
                Id = entry.Product.Id,
                CategoryKey = entry.Product.CategoryKey,
                Brand = entry.Product.Brand,
                ModelNumber = entry.Product.ModelNumber,
                Gtin = entry.Product.Gtin,
                DisplayName = entry.Product.DisplayName,
                SourceCount = entry.Analysis.SourceCount,
                AttributeCount = entry.Product.Attributes.Count,
                EvidenceCount = entry.Analysis.EvidenceCount,
                ConflictAttributeCount = entry.Analysis.ConflictAttributeCount,
                HasConflict = entry.Analysis.HasConflict,
                CompletenessScore = entry.Analysis.CompletenessScore,
                CompletenessStatus = entry.Analysis.CompletenessStatus,
                PopulatedKeyAttributeCount = entry.Analysis.PopulatedKeyAttributeCount,
                ExpectedKeyAttributeCount = entry.Analysis.ExpectedKeyAttributeCount,
                FreshnessStatus = entry.Analysis.FreshnessStatus,
                FreshnessAgeDays = entry.Analysis.FreshnessAgeDays,
                KeyAttributes = entry.Analysis.KeyAttributes,
                UpdatedUtc = entry.Product.UpdatedUtc
            }).ToArray(),
            Page = effectivePage,
            PageSize = effectivePageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / effectivePageSize)
        };
    }

    public async Task<ProductDetailResponse?> GetProductAsync(string id, CancellationToken cancellationToken)
    {
        var canonicalProduct = await canonicalProductStore.GetByIdAsync(id, cancellationToken);
        if (canonicalProduct is null)
        {
            return null;
        }

        var sourceProducts = new List<SourceProduct>();
        foreach (var sourceLink in canonicalProduct.Sources)
        {
            var sourceProduct = await sourceProductStore.GetByIdAsync(sourceLink.SourceProductId, cancellationToken)
                ?? await sourceProductStore.GetBySourceAsync(sourceLink.SourceName, sourceLink.SourceUrl, cancellationToken);

            if (sourceProduct is not null)
            {
                sourceProducts.Add(sourceProduct);
            }
        }

        var analysis = ProductAnalysisProjection.BuildSummary(canonicalProduct, categorySchemaRegistry, categoryAttributeNormaliserRegistry);

        return new ProductDetailResponse
        {
            Id = canonicalProduct.Id,
            CategoryKey = canonicalProduct.CategoryKey,
            Brand = canonicalProduct.Brand,
            ModelNumber = canonicalProduct.ModelNumber,
            Gtin = canonicalProduct.Gtin,
            DisplayName = canonicalProduct.DisplayName,
            CreatedUtc = canonicalProduct.CreatedUtc,
            UpdatedUtc = canonicalProduct.UpdatedUtc,
            SourceCount = analysis.SourceCount,
            EvidenceCount = analysis.EvidenceCount,
            ConflictAttributeCount = analysis.ConflictAttributeCount,
            HasConflict = analysis.HasConflict,
            CompletenessScore = analysis.CompletenessScore,
            CompletenessStatus = analysis.CompletenessStatus,
            PopulatedKeyAttributeCount = analysis.PopulatedKeyAttributeCount,
            ExpectedKeyAttributeCount = analysis.ExpectedKeyAttributeCount,
            FreshnessStatus = analysis.FreshnessStatus,
            FreshnessAgeDays = analysis.FreshnessAgeDays,
            KeyAttributes = analysis.KeyAttributes,
            Attributes = canonicalProduct.Attributes.Values.Select(attribute => new ProductAttributeDetailDto
            {
                AttributeKey = attribute.AttributeKey,
                Value = attribute.Value,
                ValueType = attribute.ValueType,
                Unit = attribute.Unit,
                Confidence = attribute.Confidence,
                HasConflict = attribute.HasConflict,
                Evidence = attribute.Evidence.Select(evidence => new AttributeEvidenceDto
                {
                    SourceName = evidence.SourceName,
                    SourceUrl = evidence.SourceUrl,
                    SourceProductId = evidence.SourceProductId,
                    SourceAttributeKey = evidence.SourceAttributeKey,
                    RawValue = evidence.RawValue,
                    SelectorOrPath = evidence.SelectorOrPath,
                    Confidence = evidence.Confidence,
                    ObservedUtc = evidence.ObservedUtc
                }).ToArray()
            }).OrderBy(attribute => attribute.AttributeKey).ToArray(),
            SourceProducts = sourceProducts.Select(sourceProduct => new SourceProductDetailDto
            {
                Id = sourceProduct.Id,
                SourceName = sourceProduct.SourceName,
                SourceUrl = sourceProduct.SourceUrl,
                Brand = sourceProduct.Brand,
                ModelNumber = sourceProduct.ModelNumber,
                Gtin = sourceProduct.Gtin,
                Title = sourceProduct.Title,
                RawSchemaJson = sourceProduct.RawSchemaJson,
                RawAttributes = sourceProduct.RawAttributes.Values.Select(attribute => new SourceAttributeValueDto
                {
                    AttributeKey = attribute.AttributeKey,
                    Value = attribute.Value,
                    ValueType = attribute.ValueType,
                    Unit = attribute.Unit,
                    SourcePath = attribute.SourcePath
                }).OrderBy(attribute => attribute.AttributeKey).ToArray()
            }).ToArray()
        };
    }

    public async Task<IReadOnlyList<ProductChangeEventDto>> GetProductHistoryAsync(string id, CancellationToken cancellationToken)
    {
        var history = await productChangeEventStore.GetByCanonicalProductIdAsync(id, cancellationToken: cancellationToken);
        return history.Select(changeEvent => new ProductChangeEventDto
        {
            CanonicalProductId = changeEvent.CanonicalProductId,
            CategoryKey = changeEvent.CategoryKey,
            AttributeKey = changeEvent.AttributeKey,
            OldValue = changeEvent.OldValue,
            NewValue = changeEvent.NewValue,
            SourceName = changeEvent.SourceName,
            TimestampUtc = changeEvent.TimestampUtc
        }).ToArray();
    }

    public async Task<IReadOnlyList<ConflictDto>> GetConflictsAsync(CancellationToken cancellationToken)
    {
        var cursor = await mongoDbContext.MergeConflicts.FindAsync(Builders<MergeConflict>.Filter.Empty, cancellationToken: cancellationToken);
        var conflicts = await cursor.ToListAsync(cancellationToken);
        return conflicts.OrderByDescending(conflict => conflict.CreatedUtc).Select(conflict => new ConflictDto
        {
            Id = conflict.Id,
            CanonicalProductId = conflict.CanonicalProductId,
            AttributeKey = conflict.AttributeKey,
            ExistingValue = conflict.ExistingValue,
            IncomingValue = conflict.IncomingValue,
            Reason = conflict.Reason,
            Severity = conflict.Severity,
            Status = conflict.Status,
            SuggestedValue = conflict.SuggestedValue,
            SuggestedSourceName = conflict.SuggestedSourceName,
            SuggestedConfidence = conflict.SuggestedConfidence,
            HighestConfidenceValue = conflict.HighestConfidenceValue,
            CreatedUtc = conflict.CreatedUtc,
            ResolvedUtc = conflict.ResolvedUtc
        }).ToArray();
    }

    public async Task<StatsResponse> GetStatsAsync(CancellationToken cancellationToken)
    {
        var canonicalCursor = await mongoDbContext.CanonicalProducts.FindAsync(Builders<CanonicalProduct>.Filter.Empty, cancellationToken: cancellationToken);
        var canonicalProducts = await canonicalCursor.ToListAsync(cancellationToken);

        var sourceCursor = await mongoDbContext.SourceProducts.FindAsync(Builders<SourceProduct>.Filter.Empty, cancellationToken: cancellationToken);
        var sourceProducts = await sourceCursor.ToListAsync(cancellationToken);

        var totalCanonicalProducts = canonicalProducts.Count;
        var totalSourceProducts = sourceProducts.Count;
        var averageAttributesPerProduct = totalCanonicalProducts == 0
            ? 0m
            : decimal.Round((decimal)canonicalProducts.Average(product => product.Attributes.Count), 2, MidpointRounding.AwayFromZero);
        var productsWithConflicts = canonicalProducts.Count(product => product.Attributes.Values.Any(attribute => attribute.HasConflict));
        var productsMissingKeyAttributes = canonicalProducts.Count(IsMissingKeyAttributes);

        return new StatsResponse
        {
            TotalCanonicalProducts = totalCanonicalProducts,
            TotalSourceProducts = totalSourceProducts,
            AverageAttributesPerProduct = averageAttributesPerProduct,
            PercentProductsWithConflicts = ToPercent(productsWithConflicts, totalCanonicalProducts),
            PercentProductsMissingKeyAttributes = ToPercent(productsMissingKeyAttributes, totalCanonicalProducts)
        };
    }

    private static CrawlLogDto Map(CrawlLog log)
    {
        return new CrawlLogDto
        {
            Id = log.Id,
            SourceName = log.SourceName,
            Url = log.Url,
            Status = log.Status,
            DurationMs = log.DurationMs,
            ContentHash = log.ContentHash,
            ExtractedProductCount = log.ExtractedProductCount,
            ErrorMessage = log.ErrorMessage,
            TimestampUtc = log.TimestampUtc
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

    private bool IsMissingKeyAttributes(CanonicalProduct product)
    {
        var requiredKeys = categorySchemaRegistry.GetSchema(product.CategoryKey)?.Attributes
            .Where(attribute => attribute.IsRequired)
            .Select(attribute => attribute.Key)
            ?? [];
        var keysToCheck = requiredKeys
            .Concat(categoryAttributeNormaliserRegistry.GetCompletenessAttributeKeys(product.CategoryKey))
            .Distinct(StringComparer.Ordinal);

        foreach (var key in keysToCheck)
        {
            if (key == "brand")
            {
                if (string.IsNullOrWhiteSpace(product.Brand))
                {
                    return true;
                }

                continue;
            }

            if (key == "model_number")
            {
                if (string.IsNullOrWhiteSpace(product.ModelNumber))
                {
                    return true;
                }

                continue;
            }

            if (!product.Attributes.TryGetValue(key, out var attributeValue) || attributeValue.Value is null)
            {
                return true;
            }
        }

        return false;
    }
}