namespace ProductNormaliser.Web.Contracts;

public sealed class StatsDto
{
    public int TotalCanonicalProducts { get; init; }
    public int TotalSourceProducts { get; init; }
    public decimal AverageAttributesPerProduct { get; init; }
    public decimal PercentProductsWithConflicts { get; init; }
    public decimal PercentProductsMissingKeyAttributes { get; init; }
}

public sealed class CategoryMetadataDto
{
    public string CategoryKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string FamilyKey { get; init; } = string.Empty;
    public string FamilyDisplayName { get; init; } = string.Empty;
    public string IconKey { get; init; } = string.Empty;
    public string CrawlSupportStatus { get; init; } = string.Empty;
    public decimal SchemaCompletenessScore { get; init; }
    public bool IsEnabled { get; init; }
}

public sealed class CategoryFamilyDto
{
    public string FamilyKey { get; init; } = string.Empty;
    public string FamilyDisplayName { get; init; } = string.Empty;
    public IReadOnlyList<CategoryMetadataDto> Categories { get; init; } = [];
}

public sealed class CategoryDetailDto
{
    public CategoryMetadataDto Metadata { get; init; } = new();
    public CategorySchemaDto Schema { get; init; } = new();
}

public sealed class CategorySchemaDto
{
    public string CategoryKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public IReadOnlyList<CategorySchemaAttributeDto> Attributes { get; init; } = [];
}

public sealed class CategorySchemaAttributeDto
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ValueType { get; init; } = string.Empty;
    public string? Unit { get; init; }
    public bool IsRequired { get; init; }
    public string Description { get; init; } = string.Empty;
}

public sealed class SourceDto
{
    public string SourceId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string Host { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsEnabled { get; init; }
    public IReadOnlyList<string> SupportedCategoryKeys { get; init; } = [];
    public SourceThrottlingPolicyDto ThrottlingPolicy { get; init; } = new();
    public DateTime CreatedUtc { get; init; }
    public DateTime UpdatedUtc { get; init; }
}

public sealed class SourceThrottlingPolicyDto
{
    public int MinDelayMs { get; init; }
    public int MaxDelayMs { get; init; }
    public int MaxConcurrentRequests { get; init; }
    public int RequestsPerMinute { get; init; }
    public bool RespectRobotsTxt { get; init; }
}

public sealed class RegisterSourceRequest
{
    public string SourceId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsEnabled { get; init; } = true;
    public IReadOnlyList<string> SupportedCategoryKeys { get; init; } = [];
    public SourceThrottlingPolicyDto? ThrottlingPolicy { get; init; }
}

public sealed class UpdateSourceRequest
{
    public string DisplayName { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public sealed class AssignSourceCategoriesRequest
{
    public IReadOnlyList<string> CategoryKeys { get; init; } = [];
}

public sealed class UpdateSourceThrottlingRequest
{
    public int MinDelayMs { get; init; }
    public int MaxDelayMs { get; init; }
    public int MaxConcurrentRequests { get; init; }
    public int RequestsPerMinute { get; init; }
    public bool RespectRobotsTxt { get; init; } = true;
}

public sealed class CreateCrawlJobRequest
{
    public string RequestType { get; init; } = string.Empty;
    public IReadOnlyList<string> RequestedCategories { get; init; } = [];
    public IReadOnlyList<string> RequestedSources { get; init; } = [];
    public IReadOnlyList<string> RequestedProductIds { get; init; } = [];
}

public sealed class CrawlJobQueryDto
{
    public string? Status { get; init; }
    public string? RequestType { get; init; }
    public string? CategoryKey { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public sealed class CrawlJobListResponseDto
{
    public IReadOnlyList<CrawlJobDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long TotalCount { get; init; }
    public int TotalPages { get; init; }
}

public sealed class CrawlJobDto
{
    public string JobId { get; init; } = string.Empty;
    public string RequestType { get; init; } = string.Empty;
    public IReadOnlyList<string> RequestedCategories { get; init; } = [];
    public IReadOnlyList<string> RequestedSources { get; init; } = [];
    public IReadOnlyList<string> RequestedProductIds { get; init; } = [];
    public int TotalTargets { get; init; }
    public int ProcessedTargets { get; init; }
    public int SuccessCount { get; init; }
    public int SkippedCount { get; init; }
    public int FailedCount { get; init; }
    public int CancelledCount { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime LastUpdatedAt { get; init; }
    public DateTime? EstimatedCompletion { get; init; }
    public string Status { get; init; } = string.Empty;
    public IReadOnlyList<CrawlJobCategoryBreakdownDto> PerCategoryBreakdown { get; init; } = [];
}

public sealed class CrawlJobCategoryBreakdownDto
{
    public string CategoryKey { get; init; } = string.Empty;
    public int TotalTargets { get; init; }
    public int ProcessedTargets { get; init; }
    public int SuccessCount { get; init; }
    public int SkippedCount { get; init; }
    public int FailedCount { get; init; }
    public int CancelledCount { get; init; }
}

public sealed class ProductListQueryDto
{
    public string? CategoryKey { get; init; }
    public string? Search { get; init; }
    public int? MinSourceCount { get; init; }
    public string? Freshness { get; init; }
    public string? ConflictStatus { get; init; }
    public string? CompletenessStatus { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 12;
}

public sealed class ProductListResponseDto
{
    public IReadOnlyList<ProductSummaryDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long TotalCount { get; init; }
    public int TotalPages { get; init; }
}

public sealed class ProductSummaryDto
{
    public string Id { get; init; } = string.Empty;
    public string CategoryKey { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
    public string? ModelNumber { get; init; }
    public string? Gtin { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public int SourceCount { get; init; }
    public int AttributeCount { get; init; }
    public int EvidenceCount { get; init; }
    public int ConflictAttributeCount { get; init; }
    public bool HasConflict { get; init; }
    public decimal CompletenessScore { get; init; }
    public string CompletenessStatus { get; init; } = string.Empty;
    public int PopulatedKeyAttributeCount { get; init; }
    public int ExpectedKeyAttributeCount { get; init; }
    public string FreshnessStatus { get; init; } = string.Empty;
    public int FreshnessAgeDays { get; init; }
    public IReadOnlyCollection<ProductKeyAttributeDto> KeyAttributes { get; init; } = [];
    public DateTime UpdatedUtc { get; init; }
}

public sealed class ProductDetailDto
{
    public string Id { get; init; } = string.Empty;
    public string CategoryKey { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
    public string? ModelNumber { get; init; }
    public string? Gtin { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public DateTime CreatedUtc { get; init; }
    public DateTime UpdatedUtc { get; init; }
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
    public IReadOnlyCollection<ProductKeyAttributeDto> KeyAttributes { get; init; } = [];
    public IReadOnlyCollection<ProductAttributeDetailDto> Attributes { get; init; } = [];
    public IReadOnlyCollection<SourceProductDetailDto> SourceProducts { get; init; } = [];
}

public sealed class ProductKeyAttributeDto
{
    public string AttributeKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public bool HasConflict { get; init; }
    public decimal Confidence { get; init; }
}

public sealed class ProductAttributeDetailDto
{
    public string AttributeKey { get; init; } = string.Empty;
    public object? Value { get; init; }
    public string ValueType { get; init; } = string.Empty;
    public string? Unit { get; init; }
    public decimal Confidence { get; init; }
    public bool HasConflict { get; init; }
    public IReadOnlyCollection<AttributeEvidenceDto> Evidence { get; init; } = [];
}

public sealed class AttributeEvidenceDto
{
    public string SourceName { get; init; } = string.Empty;
    public string SourceUrl { get; init; } = string.Empty;
    public string SourceProductId { get; init; } = string.Empty;
    public string SourceAttributeKey { get; init; } = string.Empty;
    public object? RawValue { get; init; }
    public string? SelectorOrPath { get; init; }
    public decimal Confidence { get; init; }
    public DateTime ObservedUtc { get; init; }
}

public sealed class SourceProductDetailDto
{
    public string Id { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public string SourceUrl { get; init; } = string.Empty;
    public string? Brand { get; init; }
    public string? ModelNumber { get; init; }
    public string? Gtin { get; init; }
    public string? Title { get; init; }
    public string RawSchemaJson { get; init; } = string.Empty;
    public IReadOnlyCollection<SourceAttributeValueDto> RawAttributes { get; init; } = [];
}

public sealed class SourceAttributeValueDto
{
    public string AttributeKey { get; init; } = string.Empty;
    public object? Value { get; init; }
    public string ValueType { get; init; } = string.Empty;
    public string? Unit { get; init; }
    public string? SourcePath { get; init; }
}

public sealed class ProductChangeEventDto
{
    public string CanonicalProductId { get; init; } = string.Empty;
    public string CategoryKey { get; init; } = string.Empty;
    public string AttributeKey { get; init; } = string.Empty;
    public object? OldValue { get; init; }
    public object? NewValue { get; init; }
    public string SourceName { get; init; } = string.Empty;
    public DateTime TimestampUtc { get; init; }
}