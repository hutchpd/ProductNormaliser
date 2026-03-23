namespace ProductNormaliser.Application.Governance;

public interface IManagementActorContext
{
    ManagementActor GetCurrentActor();
}