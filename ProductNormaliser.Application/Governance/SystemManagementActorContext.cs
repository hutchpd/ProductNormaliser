namespace ProductNormaliser.Application.Governance;

public sealed class SystemManagementActorContext : IManagementActorContext
{
    public ManagementActor GetCurrentActor()
    {
        return new ManagementActor
        {
            ActorId = "system",
            ActorType = "system",
            ActorDisplayName = "System"
        };
    }
}