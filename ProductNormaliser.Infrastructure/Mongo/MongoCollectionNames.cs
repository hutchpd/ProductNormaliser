namespace ProductNormaliser.Infrastructure.Mongo;

public static class MongoCollectionNames
{
    public const string RawPages = "raw_pages";
    public const string SourceProducts = "source_products";
    public const string CanonicalProducts = "canonical_products";
    public const string ProductOffers = "product_offers";
    public const string MergeConflicts = "merge_conflicts";
    public const string CrawlQueue = "crawl_queue";
}