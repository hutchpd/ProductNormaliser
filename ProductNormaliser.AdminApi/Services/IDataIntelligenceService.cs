using ProductNormaliser.AdminApi.Contracts;

namespace ProductNormaliser.AdminApi.Services;

public interface IDataIntelligenceService
{
    Task<DetailedCoverageResponse> GetDetailedCoverageAsync(string categoryKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<UnmappedAttributeDto>> GetUnmappedAttributesAsync(string categoryKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<SourceQualityScoreDto>> GetSourceQualityScoresAsync(string categoryKey, CancellationToken cancellationToken);
}