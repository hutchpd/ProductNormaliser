namespace ProductNormaliser.Infrastructure.Crawling;

public interface IDeltaProcessor
{
    Task<DeltaDetectionResult> DetectAsync(string sourceName, string sourceUrl, string html, CancellationToken cancellationToken);
    string ComputeHash(string html);
}