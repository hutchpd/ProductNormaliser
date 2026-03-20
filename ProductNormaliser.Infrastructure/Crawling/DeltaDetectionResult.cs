namespace ProductNormaliser.Infrastructure.Crawling;

public sealed class DeltaDetectionResult
{
    public bool IsUnchanged { get; init; }
    public string ContentHash { get; init; } = string.Empty;
}