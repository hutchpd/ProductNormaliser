namespace ProductNormaliser.Core.Models;

public sealed class ManagementAuditEntry
{
    public string Id { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string TargetType { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    public DateTime TimestampUtc { get; set; }

    public string ActorId { get; set; } = string.Empty;

    public string ActorType { get; set; } = string.Empty;

    public string? ActorDisplayName { get; set; }

    public string? ForwardedUserId { get; set; }

    public string? ForwardedUserDisplayName { get; set; }

    public Dictionary<string, string> Details { get; set; } = [];
}