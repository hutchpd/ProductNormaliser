using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Governance;

public interface IManagementAuditStore
{
    Task InsertAsync(ManagementAuditEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ManagementAuditEntry>> ListRecentAsync(int take, CancellationToken cancellationToken = default);
}