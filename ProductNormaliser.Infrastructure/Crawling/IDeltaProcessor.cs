namespace ProductNormaliser.Infrastructure.Crawling;

public interface IDeltaProcessor
{
    Task<DeltaDetectionResult> DetectAsync(string sourceName, string sourceUrl, string html, CancellationToken cancellationToken);
    Task<SemanticDeltaResult> DetectSemanticChangesAsync(ProductNormaliser.Core.Models.SourceProduct sourceProduct, CancellationToken cancellationToken);
    string ComputeHash(string html);
}