namespace ProductNormaliser.Application.Sources;

public interface IDiscoveryRunProcessor
{
    Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default);
}