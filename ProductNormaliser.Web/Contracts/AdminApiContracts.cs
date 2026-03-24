namespace ProductNormaliser.Web.Contracts;

public sealed class StatsDto
{
    public int TotalCanonicalProducts { get; init; }
    public int TotalSourceProducts { get; init; }
    public decimal AverageAttributesPerProduct { get; init; }
    public decimal PercentProductsWithConflicts { get; init; }
    public decimal PercentProductsMissingKeyAttributes { get; init; }
    public int DiscoveryQueueDepth { get; init; }
    public decimal DiscoveryProcessingRateLast24Hours { get; init; }
    public int DiscoveredUrlCountLast24Hours { get; init; }
    public int ConfirmedProductUrlCountLast24Hours { get; init; }
    public int RejectedUrlCountLast24Hours { get; init; }
    public int RobotsBlockedCountLast24Hours { get; init; }
    public int ActiveDiscoverySourceCount { get; init; }
    public OperationalSummaryDto Operational { get; init; } = new();
}

public sealed class OperationalSummaryDto
{
    public int ActiveJobCount { get; init; }
    public int QueueDepth { get; init; }
    public int RetryQueueDepth { get; init; }
    public int FailedQueueDepth { get; init; }
    public int ThroughputLast24Hours { get; init; }
    public int FailureCountLast24Hours { get; init; }
    public int HealthySourceCount { get; init; }
    public int AttentionSourceCount { get; init; }
    public IReadOnlyList<SourceOperationalMetricDto> Sources { get; init; } = [];
    public IReadOnlyList<CategoryOperationalMetricDto> Categories { get; init; } = [];
}

public sealed class SourceOperationalMetricDto
{
    public string SourceName { get; init; } = string.Empty;
    public string HealthStatus { get; init; } = string.Empty;
    public int QueueDepth { get; init; }
    public int DiscoveryQueueDepth { get; init; }
    public int RetryQueueDepth { get; init; }
    public int FailedQueueDepth { get; init; }
    public int TotalCrawlsLast24Hours { get; init; }
    public int FailedCrawlsLast24Hours { get; init; }
    public decimal FailureRateLast24Hours { get; init; }
    public int ListingPagesVisitedLast24Hours { get; init; }
    public int SitemapUrlsProcessedLast24Hours { get; init; }
    public int ConfirmedProductUrlsLast24Hours { get; init; }
    public IReadOnlyDictionary<string, decimal> DiscoveryCoverageByCategory { get; init; } = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
    public decimal TrustScore { get; init; }
    public decimal CoveragePercent { get; init; }
    public decimal SuccessfulCrawlRate { get; init; }
    public DateTime? SnapshotUtc { get; init; }
    public DateTime? LastCrawlUtc { get; init; }
    public DateTime? LastDiscoveryUtc { get; init; }
}

public sealed class CategoryOperationalMetricDto
{
    public string CategoryKey { get; init; } = string.Empty;
    public int ActiveJobCount { get; init; }
    public int QueueDepth { get; init; }
    public int RetryQueueDepth { get; init; }
    public int ThroughputLast24Hours { get; init; }
    public int CrawledProductUrlCountLast24Hours { get; init; }
    public int FailedCrawlsLast24Hours { get; init; }
    public decimal FailureRateLast24Hours { get; init; }
    public int DistinctSourceCount { get; init; }
    public int DiscoveredUrlCount { get; init; }
    public int ConfirmedProductTargetCount { get; init; }
    public int DiscoveryQueueDepth { get; init; }
    public int ActiveSourceCoverage { get; init; }
    public decimal SourceCoveragePercent { get; init; }
    public decimal DiscoveryCompletionPercent { get; init; }
}

public sealed class AnalystWorkflowDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string WorkflowType { get; init; } = string.Empty;
    public string RoutePath { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? PrimaryCategoryKey { get; init; }
    public IReadOnlyList<string> SelectedCategoryKeys { get; init; } = [];
    public IReadOnlyDictionary<string, string> State { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public DateTime CreatedUtc { get; init; }
    public DateTime UpdatedUtc { get; init; }
}

public sealed class UpsertAnalystWorkflowRequest
{
    public string? Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string WorkflowType { get; init; } = string.Empty;
    public string RoutePath { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? PrimaryCategoryKey { get; init; }
    public IReadOnlyList<string> SelectedCategoryKeys { get; init; } = [];
    public IReadOnlyDictionary<string, string> State { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed class AnalystNoteDto
{
    public string TargetType { get; init; } = string.Empty;
    public string TargetId { get; init; } = string.Empty;
    public string? Title { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTime CreatedUtc { get; init; }
    public DateTime UpdatedUtc { get; init; }
}

public sealed class UpsertAnalystNoteRequest
{
    public string TargetType { get; init; } = string.Empty;
    public string TargetId { get; init; } = string.Empty;
    public string? Title { get; init; }
    public string Content { get; init; } = string.Empty;
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
    public string ConflictSensitivity { get; init; } = string.Empty;
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
    public SourceDiscoveryProfileDto DiscoveryProfile { get; init; } = new();
    public SourceThrottlingPolicyDto ThrottlingPolicy { get; init; } = new();
    public SourceReadinessDto Readiness { get; init; } = new();
    public SourceHealthSummaryDto Health { get; init; } = new();
    public SourceLastActivityDto? LastActivity { get; init; }
    public int DiscoveryQueueDepth { get; init; }
    public int ListingPagesVisitedLast24Hours { get; init; }
    public int SitemapUrlsProcessedLast24Hours { get; init; }
    public int ConfirmedProductUrlsLast24Hours { get; init; }
    public IReadOnlyDictionary<string, decimal> DiscoveryCoverageByCategory { get; init; } = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
    public DateTime? LastDiscoveryUtc { get; init; }
    public bool SitemapReachable { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime UpdatedUtc { get; init; }
}

public sealed class SourceReadinessDto
{
    public string Status { get; init; } = string.Empty;
    public int AssignedCategoryCount { get; init; }
    public int CrawlableCategoryCount { get; init; }
    public string Summary { get; init; } = string.Empty;
}

public sealed class SourceHealthSummaryDto
{
    public string Status { get; init; } = string.Empty;
    public decimal TrustScore { get; init; }
    public decimal CoveragePercent { get; init; }
    public decimal SuccessfulCrawlRate { get; init; }
    public DateTime? SnapshotUtc { get; init; }
}

public sealed class SourceLastActivityDto
{
    public DateTime TimestampUtc { get; init; }
    public string Status { get; init; } = string.Empty;
    public long DurationMs { get; init; }
    public int ExtractedProductCount { get; init; }
    public bool HadMeaningfulChange { get; init; }
    public string? MeaningfulChangeSummary { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class SourceThrottlingPolicyDto
{
    public int MinDelayMs { get; init; }
    public int MaxDelayMs { get; init; }
    public int MaxConcurrentRequests { get; init; }
    public int RequestsPerMinute { get; init; }
    public bool RespectRobotsTxt { get; init; }
}

public sealed class SourceDiscoveryProfileDto
{
    public IReadOnlyDictionary<string, IReadOnlyList<string>> CategoryEntryPages { get; init; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> SitemapHints { get; init; } = [];
    public IReadOnlyList<string> AllowedHosts { get; init; } = [];
    public IReadOnlyList<string> AllowedPathPrefixes { get; init; } = [];
    public IReadOnlyList<string> ExcludedPathPrefixes { get; init; } = [];
    public IReadOnlyList<string> ProductUrlPatterns { get; init; } = [];
    public IReadOnlyList<string> ListingUrlPatterns { get; init; } = [];
    public int MaxDiscoveryDepth { get; init; }
    public int MaxUrlsPerRun { get; init; }
    public int MaxRetryCount { get; init; }
    public int RetryBackoffBaseMs { get; init; }
    public int RetryBackoffMaxMs { get; init; }
}

public sealed class RegisterSourceRequest
{
    public string SourceId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsEnabled { get; init; } = true;
    public IReadOnlyList<string> SupportedCategoryKeys { get; init; } = [];
    public SourceDiscoveryProfileDto? DiscoveryProfile { get; init; }
    public SourceThrottlingPolicyDto? ThrottlingPolicy { get; init; }
}

public sealed class UpdateSourceRequest
{
    public string DisplayName { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string? Description { get; init; }
    public SourceDiscoveryProfileDto? DiscoveryProfile { get; init; }
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
    public int DiscoveredUrlCount { get; init; }
    public int ConfirmedProductTargetCount { get; init; }
    public int DiscoveryQueueDepth { get; init; }
    public int ActiveSourceCoverage { get; init; }
    public decimal SourceCoveragePercent { get; init; }
    public decimal DiscoveryCompletionPercent { get; init; }
    public int CrawledProductUrlCount { get; init; }
    public int ProductQueueDepth { get; init; }
    public int ProductFailureCount { get; init; }
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
    public int DiscoveredUrlCount { get; init; }
    public int ConfirmedProductTargetCount { get; init; }
    public int DiscoveryQueueDepth { get; init; }
    public int ActiveSourceCoverage { get; init; }
    public decimal SourceCoveragePercent { get; init; }
    public decimal DiscoveryCompletionPercent { get; init; }
    public int CrawledProductUrlCount { get; init; }
    public int ProductQueueDepth { get; init; }
    public int ProductFailureCount { get; init; }
}

public sealed class ProductListQueryDto
{
    public string? CategoryKey { get; init; }
    public string? Search { get; init; }
    public int? MinSourceCount { get; init; }
    public string? Freshness { get; init; }
    public string? ConflictStatus { get; init; }
    public string? CompletenessStatus { get; init; }
    public string? Sort { get; init; }
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
    public string? RawValue { get; init; }
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
    public string? Value { get; init; }
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

public sealed class DetailedCoverageResponseDto
{
    public string CategoryKey { get; init; } = string.Empty;
    public int TotalCanonicalProducts { get; init; }
    public int TotalSourceProducts { get; init; }
    public IReadOnlyList<AttributeCoverageDetailDto> Attributes { get; init; } = [];
    public IReadOnlyList<AttributeGapDto> MostMissingAttributes { get; init; } = [];
    public IReadOnlyList<AttributeGapDto> MostConflictedAttributes { get; init; } = [];
}

public sealed class AttributeCoverageDetailDto
{
    public string AttributeKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int PresentProductCount { get; init; }
    public int MissingProductCount { get; init; }
    public decimal CoveragePercent { get; init; }
    public int ConflictProductCount { get; init; }
    public decimal ConflictPercent { get; init; }
    public decimal AverageConfidence { get; init; }
    public decimal AgreementPercent { get; init; }
    public decimal ReliabilityScore { get; init; }
}

public sealed class AttributeGapDto
{
    public string AttributeKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int ProductCount { get; init; }
    public decimal Percentage { get; init; }
}

public sealed class UnmappedAttributeDto
{
    public string CanonicalKey { get; init; } = string.Empty;
    public string RawAttributeKey { get; init; } = string.Empty;
    public int OccurrenceCount { get; init; }
    public IReadOnlyList<string> SourceNames { get; init; } = [];
    public IReadOnlyList<string> SampleValues { get; init; } = [];
    public DateTime LastSeenUtc { get; init; }
}

public sealed class MergeInsightsResponseDto
{
    public string CategoryKey { get; init; } = string.Empty;
    public IReadOnlyList<MergeConflictInsightDto> OpenConflicts { get; init; } = [];
    public IReadOnlyList<AttributeMappingSuggestionDto> AttributeSuggestions { get; init; } = [];
}

public sealed class MergeConflictInsightDto
{
    public string Id { get; init; } = string.Empty;
    public string CanonicalProductId { get; init; } = string.Empty;
    public string AttributeKey { get; init; } = string.Empty;
    public object? CurrentValue { get; init; }
    public object? IncomingValue { get; init; }
    public string Reason { get; init; } = string.Empty;
    public decimal Severity { get; init; }
    public object? SuggestedValue { get; init; }
    public string? SuggestedSourceName { get; init; }
    public decimal SuggestedConfidence { get; init; }
    public object? HighestConfidenceValue { get; init; }
    public DateTime CreatedUtc { get; init; }
}

public sealed class AttributeMappingSuggestionDto
{
    public string RawAttributeKey { get; init; } = string.Empty;
    public string SuggestedCanonicalKey { get; init; } = string.Empty;
    public decimal Confidence { get; init; }
    public int OccurrenceCount { get; init; }
    public IReadOnlyList<string> SourceNames { get; init; } = [];
}

public sealed class SourceQualityScoreDto
{
    public string SourceName { get; init; } = string.Empty;
    public int SourceProductCount { get; init; }
    public decimal AverageMappedAttributes { get; init; }
    public decimal CoveragePercent { get; init; }
    public decimal AverageAttributeConfidence { get; init; }
    public decimal AgreementPercent { get; init; }
    public decimal QualityScore { get; init; }
}

public sealed class SourceQualitySnapshotDto
{
    public string SourceName { get; init; } = string.Empty;
    public string CategoryKey { get; init; } = string.Empty;
    public DateTime TimestampUtc { get; init; }
    public decimal AttributeCoverage { get; init; }
    public decimal ConflictRate { get; init; }
    public decimal AgreementRate { get; init; }
    public decimal SuccessfulCrawlRate { get; init; }
    public decimal PriceVolatilityScore { get; init; }
    public decimal SpecStabilityScore { get; init; }
    public decimal HistoricalTrustScore { get; init; }
}

public sealed class AttributeStabilityDto
{
    public string CategoryKey { get; init; } = string.Empty;
    public string AttributeKey { get; init; } = string.Empty;
    public int ChangeCount { get; init; }
    public int OscillationCount { get; init; }
    public int DistinctValueCount { get; init; }
    public decimal StabilityScore { get; init; }
    public bool IsSuspicious { get; init; }
    public string? SuspicionReason { get; init; }
}

public sealed class SourceAttributeDisagreementDto
{
    public string SourceName { get; init; } = string.Empty;
    public string CategoryKey { get; init; } = string.Empty;
    public string AttributeKey { get; init; } = string.Empty;
    public int TotalComparisons { get; init; }
    public int TimesDisagreed { get; init; }
    public int TimesWon { get; init; }
    public decimal DisagreementRate { get; init; }
    public decimal WinRate { get; init; }
    public DateTime LastUpdatedUtc { get; init; }
}