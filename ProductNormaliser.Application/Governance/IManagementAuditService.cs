using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Governance;

public interface IManagementAuditService
{
    Task RecordAsync(
        string action,
        string targetType,
        string targetId,
        IReadOnlyDictionary<string, string>? details = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ManagementAuditEntry>> ListRecentAsync(int take = 100, CancellationToken cancellationToken = default);
}