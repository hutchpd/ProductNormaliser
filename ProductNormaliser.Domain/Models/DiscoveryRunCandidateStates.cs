namespace ProductNormaliser.Core.Models;

public static class DiscoveryRunCandidateStates
{
    public const string Pending = "pending";
    public const string Probing = "probing";
    public const string AwaitingLlm = "awaiting_llm";
    public const string Suggested = "suggested";
    public const string AutoAccepted = "auto_accepted";
    public const string ManuallyAccepted = "manually_accepted";
    public const string Dismissed = "dismissed";
    public const string Archived = "archived";
    public const string Superseded = "superseded";
    public const string Failed = "failed";
}