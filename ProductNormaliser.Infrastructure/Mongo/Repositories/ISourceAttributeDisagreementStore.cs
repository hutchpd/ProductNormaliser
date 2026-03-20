using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public interface ISourceAttributeDisagreementStore
{
    Task<SourceAttributeDisagreement?> GetAsync(string sourceName, string categoryKey, string attributeKey, CancellationToken cancellationToken = default);
    Task UpsertAsync(SourceAttributeDisagreement disagreement, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SourceAttributeDisagreement>> ListAsync(string categoryKey, string? sourceName = null, CancellationToken cancellationToken = default);
}