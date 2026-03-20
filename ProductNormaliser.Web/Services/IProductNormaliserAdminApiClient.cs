using ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Services;

public interface IProductNormaliserAdminApiClient
{
    Task<IReadOnlyList<CategoryMetadataDto>> GetEnabledCategoriesAsync(CancellationToken cancellationToken = default);

    Task<CategoryDetailDto?> GetCategoryDetailAsync(string categoryKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SourceDto>> GetSourcesAsync(CancellationToken cancellationToken = default);

    Task<SourceDto?> GetSourceAsync(string sourceId, CancellationToken cancellationToken = default);

    Task<SourceDto> RegisterSourceAsync(RegisterSourceRequest request, CancellationToken cancellationToken = default);

    Task<SourceDto> UpdateSourceAsync(string sourceId, UpdateSourceRequest request, CancellationToken cancellationToken = default);

    Task<SourceDto> EnableSourceAsync(string sourceId, CancellationToken cancellationToken = default);

    Task<SourceDto> DisableSourceAsync(string sourceId, CancellationToken cancellationToken = default);

    Task<SourceDto> AssignCategoriesAsync(string sourceId, AssignSourceCategoriesRequest request, CancellationToken cancellationToken = default);

    Task<SourceDto> UpdateThrottlingAsync(string sourceId, UpdateSourceThrottlingRequest request, CancellationToken cancellationToken = default);
}