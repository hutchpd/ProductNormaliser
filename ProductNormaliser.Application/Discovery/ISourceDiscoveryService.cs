namespace ProductNormaliser.Application.Discovery;

public interface ISourceDiscoveryService
{
    Task<SourceDiscoveryPreview> PreviewAsync(
        IReadOnlyCollection<string>? categoryKeys,
        IReadOnlyCollection<string>? sourceIds,
        CancellationToken cancellationToken = default);

    Task<SourceDiscoverySeedResult> EnsureSeededAsync(CancellationToken cancellationToken = default);

    Task<SourceDiscoverySeedResult> SeedAsync(
        IReadOnlyCollection<string>? categoryKeys,
        IReadOnlyCollection<string>? sourceIds,
        string? jobId,
        CancellationToken cancellationToken = default);
}