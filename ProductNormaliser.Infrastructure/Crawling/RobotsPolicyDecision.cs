namespace ProductNormaliser.Infrastructure.Crawling;

public sealed class RobotsPolicyDecision
{
    public bool IsAllowed { get; init; }
    public string Reason { get; init; } = string.Empty;
}