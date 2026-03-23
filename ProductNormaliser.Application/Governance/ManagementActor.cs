namespace ProductNormaliser.Application.Governance;

public sealed class ManagementActor
{
    public string ActorId { get; init; } = "system";

    public string ActorType { get; init; } = "system";

    public string? ActorDisplayName { get; init; }

    public string? ForwardedUserId { get; init; }

    public string? ForwardedUserDisplayName { get; init; }
}