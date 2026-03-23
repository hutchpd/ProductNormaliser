using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Governance;

public sealed class ManagementAuditService(
    IManagementAuditStore auditStore,
    IManagementActorContext actorContext) : IManagementAuditService
{
    public async Task RecordAsync(
        string action,
        string targetType,
        string targetId,
        IReadOnlyDictionary<string, string>? details = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);

        var actor = actorContext.GetCurrentActor();
        var entry = new ManagementAuditEntry
        {
            Id = $"audit_{Guid.NewGuid():N}",
            Action = action.Trim(),
            TargetType = targetType.Trim(),
            TargetId = targetId.Trim(),
            TimestampUtc = DateTime.UtcNow,
            ActorId = actor.ActorId,
            ActorType = actor.ActorType,
            ActorDisplayName = actor.ActorDisplayName,
            ForwardedUserId = actor.ForwardedUserId,
            ForwardedUserDisplayName = actor.ForwardedUserDisplayName,
            Details = details is null
                ? []
                : details.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase)
        };

        await auditStore.InsertAsync(entry, cancellationToken);
    }

    public Task<IReadOnlyList<ManagementAuditEntry>> ListRecentAsync(int take = 100, CancellationToken cancellationToken = default)
    {
        return auditStore.ListRecentAsync(Math.Clamp(take, 1, 500), cancellationToken);
    }
}