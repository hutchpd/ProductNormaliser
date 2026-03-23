using ProductNormaliser.AdminApi.Contracts;

namespace ProductNormaliser.AdminApi.Services;

public interface IDataIntelligenceService
{
    Task<DetailedCoverageResponse> GetDetailedCoverageAsync(string categoryKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<UnmappedAttributeDto>> GetUnmappedAttributesAsync(string categoryKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<SourceQualityScoreDto>> GetSourceQualityScoresAsync(string categoryKey, CancellationToken cancellationToken);
    Task<MergeInsightsResponse> GetMergeInsightsAsync(string categoryKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<SourceQualitySnapshotDto>> GetSourceHistoryAsync(string categoryKey, string? sourceName, int? timeRangeDays, CancellationToken cancellationToken);
    Task<IReadOnlyList<AttributeStabilityDto>> GetAttributeStabilityAsync(string categoryKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<SourceAttributeDisagreementDto>> GetSourceDisagreementsAsync(string categoryKey, string? sourceName, int? timeRangeDays, CancellationToken cancellationToken);
}