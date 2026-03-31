using MongoDB.Driver;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo;

internal static class MongoIndexCatalog
{
    public static async Task EnsureAsync(MongoDbContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        await CreateIndexesAsync(
            context.AnalystNotes,
            [
                Index(
                    Builders<AnalystNote>.IndexKeys
                        .Ascending(note => note.TargetType)
                        .Ascending(note => note.TargetId),
                    "TargetType_1_TargetId_1")
            ],
            cancellationToken);

        await CreateIndexesAsync(
            context.AnalystWorkflows,
            [
                Index(
                    Builders<AnalystWorkflow>.IndexKeys
                        .Ascending(workflow => workflow.WorkflowType)
                        .Ascending(workflow => workflow.RoutePath)
                        .Descending(workflow => workflow.UpdatedUtc),
                    "WorkflowType_1_RoutePath_1_UpdatedUtc_-1"),
                Index(
                    Builders<AnalystWorkflow>.IndexKeys
                        .Ascending(workflow => workflow.PrimaryCategoryKey)
                        .Descending(workflow => workflow.UpdatedUtc),
                    "PrimaryCategoryKey_1_UpdatedUtc_-1")
            ],
            cancellationToken);

        await CreateIndexesAsync(
            context.Categories,
            [
                Index(
                    Builders<CategoryMetadata>.IndexKeys
                        .Ascending(category => category.FamilyKey)
                        .Ascending(category => category.IsEnabled)
                        .Ascending(category => category.CrawlSupportStatus),
                    "FamilyKey_1_IsEnabled_1_CrawlSupportStatus_1")
            ],
            cancellationToken);

        await CreateIndexesAsync(
            context.CrawlJobs,
            [
                Index(
                    Builders<CrawlJob>.IndexKeys
                        .Ascending(job => job.Status)
                        .Descending(job => job.LastUpdatedAt),
                    "Status_1_LastUpdatedAt_-1"),
                Index(
                    Builders<CrawlJob>.IndexKeys
                        .Descending(job => job.StartedAt),
                    "StartedAt_-1"),
                Index(
                    Builders<CrawlJob>.IndexKeys
                        .Ascending(job => job.RequestType)
                        .Descending(job => job.LastUpdatedAt),
                    "RequestType_1_LastUpdatedAt_-1"),
                Index(
                    Builders<CrawlJob>.IndexKeys
                        .Ascending(job => job.RequestedCategories)
                        .Descending(job => job.LastUpdatedAt),
                    "RequestedCategories_1_LastUpdatedAt_-1"),
                Index(
                    Builders<CrawlJob>.IndexKeys
                        .Ascending("PerCategoryBreakdown.CategoryKey")
                        .Descending(job => job.LastUpdatedAt),
                    "PerCategoryBreakdown.CategoryKey_1_LastUpdatedAt_-1")
            ],
            cancellationToken);

        await CreateIndexesAsync(
            context.CrawlSources,
            [
                Index(
                    Builders<CrawlSource>.IndexKeys
                        .Ascending(source => source.Host)
                        .Ascending(source => source.IsEnabled),
                    "Host_1_IsEnabled_1"),
                Index(
                    Builders<CrawlSource>.IndexKeys
                        .Ascending(source => source.DisplayName),
                    "DisplayName_1")
            ],
            cancellationToken);

        await CreateIndexesAsync(
            context.DiscoveryRuns,
            [
                Index(
                    Builders<DiscoveryRun>.IndexKeys
                        .Ascending(run => run.Status)
                        .Descending(run => run.UpdatedUtc),
                    "Status_1_UpdatedUtc_-1"),
                Index(
                    Builders<DiscoveryRun>.IndexKeys
                        .Descending(run => run.CreatedUtc),
                    "CreatedUtc_-1"),
                Index(
                    Builders<DiscoveryRun>.IndexKeys
                        .Ascending(run => run.RecurringCampaignId)
                        .Ascending(run => run.Status)
                        .Descending(run => run.CreatedUtc),
                    "RecurringCampaignId_1_Status_1_CreatedUtc_-1")
            ],
            cancellationToken);

        await CreateIndexesAsync(
            context.RecurringDiscoveryCampaigns,
            [
                Index(
                    Builders<RecurringDiscoveryCampaign>.IndexKeys
                        .Ascending(campaign => campaign.Status)
                        .Ascending(campaign => campaign.NextScheduledUtc),
                    "Status_1_NextScheduledUtc_1"),
                Index(
                    Builders<RecurringDiscoveryCampaign>.IndexKeys
                        .Ascending(campaign => campaign.CampaignFingerprint),
                    "CampaignFingerprint_1",
                    unique: true)
            ],
            cancellationToken);

        await CreateIndexesAsync(
            context.DiscoveryRunCandidates,
            [
                Index(
                    Builders<DiscoveryRunCandidate>.IndexKeys
                        .Ascending(candidate => candidate.RunId)
                        .Ascending(candidate => candidate.CandidateKey),
                    "RunId_1_CandidateKey_1",
                    unique: true),
                Index(
                    Builders<DiscoveryRunCandidate>.IndexKeys
                        .Ascending(candidate => candidate.RunId)
                        .Ascending(candidate => candidate.State)
                        .Descending(candidate => candidate.UpdatedUtc),
                    "RunId_1_State_1_UpdatedUtc_-1"),
                Index(
                    Builders<DiscoveryRunCandidate>.IndexKeys
                        .Ascending(candidate => candidate.RunId)
                        .Descending(candidate => candidate.ConfidenceScore),
                    "RunId_1_ConfidenceScore_-1")
            ],
            cancellationToken);

        await CreateIndexesAsync(
            context.DiscoveryRunCandidateDispositions,
            [
                Index(
                    Builders<DiscoveryRunCandidateDisposition>.IndexKeys
                        .Ascending(disposition => disposition.IsActive)
                        .Ascending(disposition => disposition.ScopeFingerprint)
                        .Ascending(disposition => disposition.NormalizedHost)
                        .Descending(disposition => disposition.UpdatedUtc),
                    "IsActive_1_ScopeFingerprint_1_NormalizedHost_1_UpdatedUtc_-1"),
                Index(
                    Builders<DiscoveryRunCandidateDisposition>.IndexKeys
                        .Ascending(disposition => disposition.IsActive)
                        .Ascending(disposition => disposition.ScopeFingerprint)
                        .Ascending(disposition => disposition.NormalizedBaseUrl)
                        .Descending(disposition => disposition.UpdatedUtc),
                    "IsActive_1_ScopeFingerprint_1_NormalizedBaseUrl_1_UpdatedUtc_-1"),
                Index(
                    Builders<DiscoveryRunCandidateDisposition>.IndexKeys
                        .Ascending(disposition => disposition.IsActive)
                        .Ascending(disposition => disposition.ScopeFingerprint)
                        .Ascending(disposition => disposition.NormalizedDisplayName)
                        .Descending(disposition => disposition.UpdatedUtc),
                    "IsActive_1_ScopeFingerprint_1_NormalizedDisplayName_1_UpdatedUtc_-1")
            ],
            cancellationToken);

        await CreateIndexesAsync(
            context.DiscoveryQueueItems,
            [
                Index(
                    Builders<DiscoveryQueueItem>.IndexKeys
                        .Ascending(item => item.JobId)
                        .Ascending(item => item.State),
                    "JobId_1_State_1"),
                Index(
                    Builders<DiscoveryQueueItem>.IndexKeys
                        .Ascending(item => item.State)
                        .Ascending(item => item.NextAttemptUtc)
                        .Ascending(item => item.EnqueuedUtc),
                    "State_1_NextAttemptUtc_1_EnqueuedUtc_1"),
                Index(
                    Builders<DiscoveryQueueItem>.IndexKeys
                        .Ascending(item => item.SourceId)
                        .Ascending(item => item.CategoryKey)
                        .Ascending(item => item.State),
                    "SourceId_1_CategoryKey_1_State_1")
            ],
            cancellationToken);

        await CreateIndexesAsync(
            context.DiscoveredUrls,
            [
                Index(
                    Builders<DiscoveredUrl>.IndexKeys
                        .Ascending(item => item.SourceId)
                        .Ascending(item => item.CategoryKey)
                        .Ascending(item => item.NormalizedUrl),
                    "SourceId_1_CategoryKey_1_NormalizedUrl_1",
                    unique: true),
                Index(
                    Builders<DiscoveredUrl>.IndexKeys
                        .Ascending(item => item.JobId)
                        .Ascending(item => item.State),
                    "JobId_1_State_1"),
                Index(
                    Builders<DiscoveredUrl>.IndexKeys
                        .Ascending(item => item.State)
                        .Ascending(item => item.LastProcessedUtc)
                        .Ascending(item => item.NextAttemptUtc),
                    "State_1_LastProcessedUtc_1_NextAttemptUtc_1"),
                Index(
                    Builders<DiscoveredUrl>.IndexKeys
                        .Ascending(item => item.SourceId)
                        .Ascending(item => item.CategoryKey)
                        .Ascending(item => item.JobId),
                    "SourceId_1_CategoryKey_1_JobId_1"),
                Index(
                    Builders<DiscoveredUrl>.IndexKeys
                        .Ascending(item => item.SourceId)
                        .Ascending(item => item.CategoryKey)
                        .Descending(item => item.LastSeenUtc),
                    "SourceId_1_CategoryKey_1_LastSeenUtc_-1")
            ],
            cancellationToken);

        await CreateIndexesAsync(
            context.CanonicalProducts,
            [
                Index(
                    Builders<CanonicalProduct>.IndexKeys
                        .Ascending(product => product.Gtin),
                    "Gtin_1"),
                Index(
                    Builders<CanonicalProduct>.IndexKeys
                        .Ascending(product => product.Brand)
                        .Ascending(product => product.ModelNumber),
                    "Brand_1_ModelNumber_1"),
                Index(
                    Builders<CanonicalProduct>.IndexKeys
                        .Ascending(product => product.CategoryKey)
                        .Ascending(product => product.Brand),
                    "CategoryKey_1_Brand_1"),
                Index(
                    Builders<CanonicalProduct>.IndexKeys
                        .Ascending(product => product.CategoryKey)
                        .Descending(product => product.UpdatedUtc),
                    "CategoryKey_1_UpdatedUtc_-1")
            ],
            cancellationToken);

        await CreateIndexesAsync(
            context.SourceProducts,
            [
                Index(
                    Builders<SourceProduct>.IndexKeys
                        .Ascending(product => product.SourceName)
                        .Ascending(product => product.SourceUrl),
                    "SourceName_1_SourceUrl_1"),
                Index(
                    Builders<SourceProduct>.IndexKeys
                        .Ascending(product => product.SourceName)
                        .Ascending(product => product.CategoryKey)
                        .Descending(product => product.FetchedUtc),
                    "SourceName_1_CategoryKey_1_FetchedUtc_-1")
            ],
            cancellationToken);

        await CreateIndexesAsync(
            context.ProductOffers,
            [
                Index(
                    Builders<ProductOffer>.IndexKeys
                        .Ascending(offer => offer.CanonicalProductId),
                    "CanonicalProductId_1")
            ],
            cancellationToken);

        await CreateIndexesAsync(
            context.MergeConflicts,
            [
                Index(
                    Builders<MergeConflict>.IndexKeys
                        .Ascending(conflict => conflict.CanonicalProductId)
                        .Ascending(conflict => conflict.Status),
                    "CanonicalProductId_1_Status_1"),
                Index(
                    Builders<MergeConflict>.IndexKeys
                        .Ascending(conflict => conflict.Status)
                        .Descending(conflict => conflict.CreatedUtc),
                    "Status_1_CreatedUtc_-1")
            ],
            cancellationToken);

        await CreateIndexesAsync(
            context.CrawlQueueItems,
            [
                Index(
                    Builders<CrawlQueueItem>.IndexKeys
                        .Ascending(item => item.Status)
                        .Ascending(item => item.NextAttemptUtc)
                        .Ascending(item => item.EnqueuedUtc),
                    "Status_1_NextAttemptUtc_1_EnqueuedUtc_1"),
                Index(
                    Builders<CrawlQueueItem>.IndexKeys
                        .Ascending(item => item.JobId)
                        .Ascending(item => item.Status),
                    "JobId_1_Status_1"),
                Index(
                    Builders<CrawlQueueItem>.IndexKeys
                        .Ascending(item => item.InitiatingJobId),
                    "InitiatingJobId_1")
            ],
            cancellationToken);

        await CreateIndexesAsync(
            context.CrawlLogs,
            [
                Index(
                    Builders<CrawlLog>.IndexKeys
                        .Descending(log => log.TimestampUtc),
                    "TimestampUtc_-1"),
                Index(
                    Builders<CrawlLog>.IndexKeys
                        .Ascending(log => log.SourceName)
                        .Descending(log => log.TimestampUtc),
                    "SourceName_1_TimestampUtc_-1"),
                Index(
                    Builders<CrawlLog>.IndexKeys
                        .Ascending(log => log.SourceName)
                        .Ascending(log => log.Url)
                        .Descending(log => log.TimestampUtc),
                    "SourceName_1_Url_1_TimestampUtc_-1")
            ],
            cancellationToken);

        await CreateIndexesAsync(
            context.UnmappedAttributes,
            [
                Index(
                    Builders<UnmappedAttribute>.IndexKeys
                        .Ascending(attribute => attribute.CategoryKey)
                        .Ascending(attribute => attribute.OccurrenceCount),
                    "CategoryKey_1_OccurrenceCount_1")
            ],
            cancellationToken);

        await CreateIndexesAsync(
            context.SourceQualitySnapshots,
            [
                Index(
                    Builders<SourceQualitySnapshot>.IndexKeys
                        .Ascending(snapshot => snapshot.SourceName)
                        .Ascending(snapshot => snapshot.CategoryKey)
                        .Descending(snapshot => snapshot.TimestampUtc),
                    "SourceName_1_CategoryKey_1_TimestampUtc_-1"),
                Index(
                    Builders<SourceQualitySnapshot>.IndexKeys
                        .Ascending(snapshot => snapshot.CategoryKey)
                        .Descending(snapshot => snapshot.TimestampUtc),
                    "CategoryKey_1_TimestampUtc_-1")
            ],
            cancellationToken);

        await CreateIndexesAsync(
            context.ProductChangeEvents,
            [
                Index(
                    Builders<ProductChangeEvent>.IndexKeys
                        .Ascending(changeEvent => changeEvent.CanonicalProductId)
                        .Descending(changeEvent => changeEvent.TimestampUtc),
                    "CanonicalProductId_1_TimestampUtc_-1"),
                Index(
                    Builders<ProductChangeEvent>.IndexKeys
                        .Ascending(changeEvent => changeEvent.CategoryKey)
                        .Descending(changeEvent => changeEvent.TimestampUtc),
                    "CategoryKey_1_TimestampUtc_-1"),
                Index(
                    Builders<ProductChangeEvent>.IndexKeys
                        .Ascending(changeEvent => changeEvent.SourceName)
                        .Ascending(changeEvent => changeEvent.CategoryKey)
                        .Descending(changeEvent => changeEvent.TimestampUtc),
                    "SourceName_1_CategoryKey_1_TimestampUtc_-1")
            ],
            cancellationToken);

        await CreateIndexesAsync(
            context.AdaptiveCrawlPolicies,
            [
                Index(
                    Builders<AdaptiveCrawlPolicy>.IndexKeys
                        .Ascending(policy => policy.SourceName)
                        .Ascending(policy => policy.CategoryKey),
                    "SourceName_1_CategoryKey_1")
            ],
            cancellationToken);

        await CreateIndexesAsync(
            context.SourceAttributeDisagreements,
            [
                Index(
                    Builders<SourceAttributeDisagreement>.IndexKeys
                        .Ascending(disagreement => disagreement.SourceName)
                        .Ascending(disagreement => disagreement.CategoryKey)
                        .Ascending(disagreement => disagreement.AttributeKey),
                    "SourceName_1_CategoryKey_1_AttributeKey_1"),
                Index(
                    Builders<SourceAttributeDisagreement>.IndexKeys
                        .Ascending(disagreement => disagreement.CategoryKey)
                        .Descending(disagreement => disagreement.DisagreementRate)
                        .Ascending(disagreement => disagreement.SourceName),
                    "CategoryKey_1_DisagreementRate_-1_SourceName_1")
            ],
            cancellationToken);

        await CreateIndexesAsync(
            context.ManagementAuditEntries,
            [
                Index(
                    Builders<ManagementAuditEntry>.IndexKeys
                        .Descending(entry => entry.TimestampUtc),
                    "TimestampUtc_-1"),
                Index(
                    Builders<ManagementAuditEntry>.IndexKeys
                        .Ascending(entry => entry.TargetType)
                        .Ascending(entry => entry.TargetId)
                        .Descending(entry => entry.TimestampUtc),
                    "TargetType_1_TargetId_1_TimestampUtc_-1")
            ],
            cancellationToken);
    }

    private static Task CreateIndexesAsync<TDocument>(
        IMongoCollection<TDocument> collection,
        IReadOnlyCollection<CreateIndexModel<TDocument>> indexes,
        CancellationToken cancellationToken)
    {
        return collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private static CreateIndexModel<TDocument> Index<TDocument>(
        IndexKeysDefinition<TDocument> keys,
        string name,
        bool unique = false)
    {
        return new CreateIndexModel<TDocument>(
            keys,
            new CreateIndexOptions
            {
                Name = name,
                Unique = unique
            });
    }
}