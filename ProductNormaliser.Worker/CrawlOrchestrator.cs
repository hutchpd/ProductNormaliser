using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Application.Observability;
using ProductNormaliser.Infrastructure.Crawling;
using ProductNormaliser.Infrastructure.Discovery;
using ProductNormaliser.Infrastructure.Mongo.Repositories;
using ProductNormaliser.Infrastructure.StructuredData;
using System.Diagnostics;

namespace ProductNormaliser.Worker;

public sealed class CrawlOrchestrator(
    IRobotsPolicyService robotsPolicyService,
    IHttpFetcher httpFetcher,
    IDeltaProcessor deltaProcessor,
    ISourceTrustService sourceTrustService,
    ISourceDisagreementService sourceDisagreementService,
    IRawPageStore rawPageStore,
    IStructuredDataExtractor structuredDataExtractor,
    ISourceProductBuilder sourceProductBuilder,
    IAttributeNormaliser attributeNormaliser,
    ISourceProductStore sourceProductStore,
    ICanonicalProductStore canonicalProductStore,
    IProductChangeEventStore productChangeEventStore,
    IProductIdentityResolver productIdentityResolver,
    ICanonicalMergeService canonicalMergeService,
    IProductOfferStore productOfferStore,
    IConflictDetector conflictDetector,
    IMergeConflictStore mergeConflictStore,
    IRelatedLinkExpansionService relatedLinkExpansionService,
    ICrawlLogStore crawlLogStore,
    ILogger<CrawlOrchestrator> logger)
{
    public async Task<CrawlProcessResult> ProcessAsync(CrawlTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        var stopwatch = Stopwatch.StartNew();
        using var activity = ProductNormaliserTelemetry.ActivitySource.StartActivity("crawl.target.process", ActivityKind.Internal);

        var sourceName = GetSourceName(target);
        string? contentHash = null;
        var extractedProductCount = 0;
        SemanticDeltaResult? semanticDelta = null;
        var queueItemId = target.Metadata.TryGetValue("queueItemId", out var resolvedQueueItemId) ? resolvedQueueItemId : null;

        activity?.SetTag("crawl.source", sourceName);
        activity?.SetTag("crawl.category", target.CategoryKey);
        activity?.SetTag("crawl.url", target.Url);
        activity?.SetTag("crawl.queue_item_id", queueItemId);

        logger.LogInformation(
            "Processing crawl target {SourceName} {CategoryKey} {Url} from queue item {QueueItemId}",
            sourceName,
            target.CategoryKey,
            target.Url,
            queueItemId ?? "n/a");

        try
        {
            var robotsDecision = await robotsPolicyService.EvaluateAsync(target, cancellationToken);
            if (!robotsDecision.IsAllowed)
            {
                logger.LogInformation("Skipping {Url}: {Reason}", target.Url, robotsDecision.Reason);
                var result = CrawlProcessResult.Skipped(robotsDecision.Reason);
                await WriteCrawlLogAsync(sourceName, target.Url, result, stopwatch.ElapsedMilliseconds, cancellationToken);
                RecordTelemetry(result, sourceName, target.CategoryKey, stopwatch.ElapsedMilliseconds);
                return result;
            }

            var fetchResult = await httpFetcher.FetchAsync(target, cancellationToken);
            if (!fetchResult.IsSuccess || string.IsNullOrWhiteSpace(fetchResult.Html))
            {
                logger.LogWarning("Fetch failed for {Url}: {Reason}", target.Url, fetchResult.FailureReason ?? "Unknown fetch failure.");
                var result = CrawlProcessResult.Failed(fetchResult.FailureReason ?? "Fetch failed.");
                await WriteCrawlLogAsync(sourceName, target.Url, result, stopwatch.ElapsedMilliseconds, cancellationToken);
                RecordTelemetry(result, sourceName, target.CategoryKey, stopwatch.ElapsedMilliseconds);
                return result;
            }

            var delta = await deltaProcessor.DetectAsync(sourceName, target.Url, fetchResult.Html, cancellationToken);
            contentHash = delta.ContentHash;
            await rawPageStore.UpsertAsync(BuildRawPage(target, sourceName, fetchResult, delta.ContentHash), cancellationToken);

            if (delta.IsUnchanged)
            {
                logger.LogInformation("Skipping {Url}: unchanged page content detected", target.Url);
                var result = CrawlProcessResult.Skipped("Unchanged page content.", contentHash);
                await WriteCrawlLogAsync(sourceName, target.Url, result, stopwatch.ElapsedMilliseconds, cancellationToken);
                RecordTelemetry(result, sourceName, target.CategoryKey, stopwatch.ElapsedMilliseconds);
                return result;
            }

            var extractedProducts = structuredDataExtractor.ExtractProducts(fetchResult.Html, target.Url);
            extractedProductCount = extractedProducts.Count;
            if (extractedProducts.Count == 0)
            {
                logger.LogInformation("No structured products found for {Url}", target.Url);
                var result = CrawlProcessResult.Completed("No structured products found.", contentHash);
                await WriteCrawlLogAsync(sourceName, target.Url, result, stopwatch.ElapsedMilliseconds, cancellationToken);
                RecordTelemetry(result, sourceName, target.CategoryKey, stopwatch.ElapsedMilliseconds);
                return result;
            }

            var processedProductCount = 0;

            foreach (var extractedProduct in extractedProducts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sourceProduct = sourceProductBuilder.Build(sourceName, target.CategoryKey, extractedProduct, fetchResult.FetchedUtc);
                sourceProduct.NormalisedAttributes = attributeNormaliser.Normalise(sourceProduct.CategoryKey, sourceProduct.RawAttributes);
                semanticDelta = await deltaProcessor.DetectSemanticChangesAsync(sourceProduct, cancellationToken);

                await sourceProductStore.UpsertAsync(sourceProduct, cancellationToken);

                var identityMatch = await ResolveIdentityAsync(sourceProduct, cancellationToken);
                var existingCanonical = identityMatch.CanonicalProductId is null
                    ? null
                    : await canonicalProductStore.GetByIdAsync(identityMatch.CanonicalProductId, cancellationToken);

                var canonicalProduct = canonicalMergeService.Merge(existingCanonical, sourceProduct);
                await canonicalProductStore.UpsertAsync(canonicalProduct, cancellationToken);
                sourceDisagreementService.RefreshForProduct(canonicalProduct);

                var changeEvents = deltaProcessor.BuildChangeEvents(existingCanonical, canonicalProduct, sourceProduct, semanticDelta);
                await productChangeEventStore.InsertManyAsync(changeEvents, cancellationToken);

                foreach (var offer in sourceProduct.Offers)
                {
                    offer.CanonicalProductId = canonicalProduct.Id;
                    await productOfferStore.UpsertAsync(offer, cancellationToken);
                }

                var conflicts = conflictDetector.Detect(canonicalProduct);
                foreach (var conflict in conflicts)
                {
                    await mergeConflictStore.UpsertAsync(conflict, cancellationToken);
                }

                processedProductCount += 1;
            }

            if (processedProductCount > 0)
            {
                await ExpandRelatedLinksSafelyAsync(target, fetchResult.Html!, cancellationToken);
                sourceTrustService.CaptureSnapshot(sourceName, target.CategoryKey);
            }

            logger.LogInformation(
                "Completed crawl for source {SourceName} category {CategoryKey} url {Url}; processed={ProcessedProductCount}, extracted={ExtractedProductCount}, hadMeaningfulChange={HadMeaningfulChange}",
                sourceName,
                target.CategoryKey,
                target.Url,
                processedProductCount,
                extractedProductCount,
                semanticDelta?.HasMeaningfulChanges ?? false);
            var completedResult = CrawlProcessResult.Completed($"Processed {processedProductCount} product(s).", contentHash, extractedProductCount);
            await WriteCrawlLogAsync(sourceName, target.Url, completedResult, stopwatch.ElapsedMilliseconds, semanticDelta, cancellationToken);
            RecordTelemetry(completedResult, sourceName, target.CategoryKey, stopwatch.ElapsedMilliseconds);
            return completedResult;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled crawl orchestration failure for {Url}", target.Url);
            var result = CrawlProcessResult.Failed(exception.Message, contentHash, extractedProductCount);
            await WriteCrawlLogAsync(sourceName, target.Url, result, stopwatch.ElapsedMilliseconds, semanticDelta, cancellationToken);
            RecordTelemetry(result, sourceName, target.CategoryKey, stopwatch.ElapsedMilliseconds);
            return result;
        }
    }

    private static void RecordTelemetry(CrawlProcessResult result, string sourceName, string categoryKey, long durationMs)
    {
        var tags = new TagList
        {
            { "source", sourceName },
            { "category", categoryKey },
            { "status", result.Status }
        };

        ProductNormaliserTelemetry.CrawlTargetsProcessed.Add(1, tags);
        ProductNormaliserTelemetry.CrawlProductsExtracted.Add(result.ExtractedProductCount, tags);
        ProductNormaliserTelemetry.CrawlTargetDurationMs.Record(durationMs, tags);
    }

    private async Task<ProductIdentityMatchResult> ResolveIdentityAsync(SourceProduct sourceProduct, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(sourceProduct.Gtin))
        {
            var gtinCandidate = await canonicalProductStore.GetByGtinAsync(sourceProduct.Gtin, cancellationToken);
            if (gtinCandidate is not null)
            {
                return productIdentityResolver.Match(sourceProduct, [gtinCandidate]);
            }
        }

        if (!string.IsNullOrWhiteSpace(sourceProduct.Brand) && !string.IsNullOrWhiteSpace(sourceProduct.ModelNumber))
        {
            var brandModelCandidate = await canonicalProductStore.GetByBrandAndModelAsync(sourceProduct.Brand, sourceProduct.ModelNumber, cancellationToken);
            if (brandModelCandidate is not null)
            {
                return productIdentityResolver.Match(sourceProduct, [brandModelCandidate]);
            }
        }

        var candidates = await canonicalProductStore.ListPotentialMatchesAsync(sourceProduct.CategoryKey, sourceProduct.Brand, cancellationToken);
        return productIdentityResolver.Match(sourceProduct, candidates);
    }

    private static string GetSourceName(CrawlTarget target)
    {
        return target.Metadata.TryGetValue("sourceName", out var sourceName) && !string.IsNullOrWhiteSpace(sourceName)
            ? sourceName
            : "unknown-source";
    }

    private static RawPage BuildRawPage(CrawlTarget target, string sourceName, FetchResult fetchResult, string contentHash)
    {
        var hashSegment = contentHash.Length > 12
            ? contentHash[..12]
            : contentHash;

        return new RawPage
        {
            Id = $"{sourceName}:{hashSegment}:{fetchResult.FetchedUtc:yyyyMMddHHmmss}",
            SourceName = sourceName,
            SourceUrl = target.Url,
            CategoryKey = target.CategoryKey,
            Html = fetchResult.Html ?? string.Empty,
            ContentHash = contentHash,
            StatusCode = fetchResult.StatusCode,
            ContentType = "text/html",
            FetchedUtc = fetchResult.FetchedUtc
        };
    }

    private async Task ExpandRelatedLinksSafelyAsync(CrawlTarget target, string html, CancellationToken cancellationToken)
    {
        try
        {
            var expansion = await relatedLinkExpansionService.ExpandAsync(target, html, cancellationToken);
            if (expansion.TotalEnqueued > 0)
            {
                logger.LogInformation(
                    "Expanded related discovery links for {Url}; enqueuedProducts={EnqueuedProducts}, enqueuedListings={EnqueuedListings}",
                    target.Url,
                    expansion.EnqueuedProductUrls,
                    expansion.EnqueuedListingUrls);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Related link expansion failed for {Url}", target.Url);
        }
    }

    private async Task WriteCrawlLogAsync(string sourceName, string url, CrawlProcessResult result, long durationMs, CancellationToken cancellationToken)
    {
        await WriteCrawlLogAsync(sourceName, url, result, durationMs, null, cancellationToken);
    }

    private async Task WriteCrawlLogAsync(string sourceName, string url, CrawlProcessResult result, long durationMs, SemanticDeltaResult? semanticDelta, CancellationToken cancellationToken)
    {
        await crawlLogStore.InsertAsync(new CrawlLog
        {
            Id = $"crawl:{Guid.NewGuid():N}",
            SourceName = sourceName,
            Url = url,
            Status = result.Status,
            DurationMs = durationMs,
            ContentHash = result.ContentHash,
            ExtractedProductCount = result.ExtractedProductCount,
            HadMeaningfulChange = semanticDelta?.HasMeaningfulChanges ?? false,
            MeaningfulChangeSummary = semanticDelta?.Summary,
            ErrorMessage = result.Status == "failed" ? result.Message : null,
            TimestampUtc = DateTime.UtcNow
        }, cancellationToken);
    }
}