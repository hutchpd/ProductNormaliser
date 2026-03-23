using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Analyst;

public interface IAnalystNoteStore
{
    Task<AnalystNote?> GetAsync(string targetType, string targetId, CancellationToken cancellationToken = default);
    Task UpsertAsync(AnalystNote note, CancellationToken cancellationToken = default);
    Task DeleteAsync(string targetType, string targetId, CancellationToken cancellationToken = default);
}