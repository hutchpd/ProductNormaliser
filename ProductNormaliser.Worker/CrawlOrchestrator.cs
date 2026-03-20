using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Crawling;
using ProductNormaliser.Infrastructure.Mongo.Repositories;
using ProductNormaliser.Infrastructure.StructuredData;

namespace ProductNormaliser.Worker;

public sealed class CrawlOrchestrator(
    IRobotsPolicyService robotsPolicyService,
    IHttpFetcher httpFetcher,
    IDeltaProcessor deltaProcessor,
    IRawPageStore rawPageStore,
    IStructuredDataExtractor structuredDataExtractor,
    ISourceProductBuilder sourceProductBuilder,
    IAttributeNormaliser attributeNormaliser,
    ISourceProductStore sourceProductStore,
    ICanonicalProductStore canonicalProductStore,
    IProductIdentityResolver productIdentityResolver,
    ICanonicalMergeService canonicalMergeService,
    IProductOfferStore productOfferStore,
    IConflictDetector conflictDetector,
    IMergeConflictStore mergeConflictStore,
    ILogger<CrawlOrchestrator> logger)
{
    public async Task<CrawlProcessResult> ProcessAsync(CrawlTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        var sourceName = GetSourceName(target);

        logger.LogInformation("Processing crawl target {SourceName} {Url}", sourceName, target.Url);

        var robotsDecision = await robotsPolicyService.EvaluateAsync(target, cancellationToken);
        if (!robotsDecision.IsAllowed)
        {
            logger.LogInformation("Skipping {Url}: {Reason}", target.Url, robotsDecision.Reason);
            return CrawlProcessResult.Skipped(robotsDecision.Reason);
        }

        var fetchResult = await httpFetcher.FetchAsync(target, cancellationToken);
        if (!fetchResult.IsSuccess || string.IsNullOrWhiteSpace(fetchResult.Html))
        {
            logger.LogWarning("Fetch failed for {Url}: {Reason}", target.Url, fetchResult.FailureReason ?? "Unknown fetch failure.");
            return CrawlProcessResult.Failed(fetchResult.FailureReason ?? "Fetch failed.");
        }

        var delta = await deltaProcessor.DetectAsync(sourceName, target.Url, fetchResult.Html, cancellationToken);
        await rawPageStore.UpsertAsync(BuildRawPage(target, sourceName, fetchResult, delta.ContentHash), cancellationToken);

        if (delta.IsUnchanged)
        {
            logger.LogInformation("Skipping {Url}: unchanged page content detected", target.Url);
            return CrawlProcessResult.Skipped("Unchanged page content.");
        }

        var extractedProducts = structuredDataExtractor.ExtractProducts(fetchResult.Html, target.Url);
        if (extractedProducts.Count == 0)
        {
            logger.LogInformation("No structured products found for {Url}", target.Url);
            return CrawlProcessResult.Completed("No structured products found.");
        }

        var processedProductCount = 0;

        foreach (var extractedProduct in extractedProducts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceProduct = sourceProductBuilder.Build(sourceName, target.CategoryKey, extractedProduct, fetchResult.FetchedUtc);
            sourceProduct.NormalisedAttributes = attributeNormaliser.Normalise(sourceProduct.CategoryKey, sourceProduct.RawAttributes);

            await sourceProductStore.UpsertAsync(sourceProduct, cancellationToken);

            var identityMatch = await ResolveIdentityAsync(sourceProduct, cancellationToken);
            var existingCanonical = identityMatch.CanonicalProductId is null
                ? null
                : await canonicalProductStore.GetByIdAsync(identityMatch.CanonicalProductId, cancellationToken);

            var canonicalProduct = canonicalMergeService.Merge(existingCanonical, sourceProduct);
            await canonicalProductStore.UpsertAsync(canonicalProduct, cancellationToken);

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

        logger.LogInformation("Completed crawl for {Url}; processed {ProductCount} product(s)", target.Url, processedProductCount);
        return CrawlProcessResult.Completed($"Processed {processedProductCount} product(s).");
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
}