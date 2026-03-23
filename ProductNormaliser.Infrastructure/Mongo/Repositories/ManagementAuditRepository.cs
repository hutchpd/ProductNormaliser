using MongoDB.Driver;
using ProductNormaliser.Application.Governance;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Infrastructure.Mongo.Repositories;

public sealed class ManagementAuditRepository(MongoDbContext context)
    : MongoRepositoryBase<ManagementAuditEntry>(context.ManagementAuditEntries), IManagementAuditStore
{
    public override async Task InsertAsync(ManagementAuditEntry entry, CancellationToken cancellationToken = default)
    {
        await base.InsertAsync(entry, cancellationToken);
    }

    public async Task<IReadOnlyList<ManagementAuditEntry>> ListRecentAsync(int take, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(Builders<ManagementAuditEntry>.Filter.Empty)
            .SortByDescending(entry => entry.TimestampUtc)
            .Limit(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);
    }
}