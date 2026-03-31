namespace ProductNormaliser.Application.Sources;

public sealed class SourceRecurringDiscoveryHistory
{
    public int DiscoveryRunCount { get; init; }
    public int AcceptedCandidateCount { get; init; }
    public int SuggestedCandidateCount { get; init; }
    public int DismissedCandidateCount { get; init; }
    public int SupersededCandidateCount { get; init; }
    public DateTime? LastAcceptedUtc { get; init; }

    public decimal AcceptanceRate => DiscoveryRunCount == 0
        ? 0m
        : decimal.Round((decimal)AcceptedCandidateCount / DiscoveryRunCount, 4, MidpointRounding.AwayFromZero);
}