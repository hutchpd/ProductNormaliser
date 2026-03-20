using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Crawling;

public interface IDeltaProcessor
{
    Task<DeltaDetectionResult> DetectAsync(string sourceName, string sourceUrl, string html, CancellationToken cancellationToken);
    Task<SemanticDeltaResult> DetectSemanticChangesAsync(SourceProduct sourceProduct, CancellationToken cancellationToken);
    IReadOnlyList<ProductChangeEvent> BuildChangeEvents(CanonicalProduct? previousCanonical, CanonicalProduct currentCanonical, SourceProduct sourceProduct, SemanticDeltaResult semanticDelta);
    string ComputeHash(string html);
}