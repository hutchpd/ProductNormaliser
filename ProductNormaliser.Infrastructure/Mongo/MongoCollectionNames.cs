namespace ProductNormaliser.Infrastructure.Mongo;

public static class MongoCollectionNames
{
    public const string Categories = "category_metadata";
    public const string CrawlSources = "crawl_sources";
    public const string RawPages = "raw_pages";
    public const string SourceProducts = "source_products";
    public const string CanonicalProducts = "canonical_products";
    public const string ProductOffers = "product_offers";
    public const string MergeConflicts = "merge_conflicts";
    public const string CrawlQueue = "crawl_queue";
    public const string CrawlLogs = "crawl_logs";
    public const string UnmappedAttributes = "unmapped_attributes";
    public const string SourceQualitySnapshots = "source_quality_snapshots";
    public const string ProductChangeEvents = "product_change_events";
    public const string AdaptiveCrawlPolicies = "adaptive_crawl_policies";
    public const string SourceAttributeDisagreements = "source_attribute_disagreements";
}