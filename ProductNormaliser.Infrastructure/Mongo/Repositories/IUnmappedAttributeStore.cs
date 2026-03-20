using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public interface IUnmappedAttributeStore
{
    Task<IReadOnlyList<UnmappedAttribute>> ListAsync(string? categoryKey = null, CancellationToken cancellationToken = default);
}