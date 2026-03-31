namespace ProductNormaliser.Core.Models;

public sealed class RecurringDiscoveryCampaignMemory
{
    public int HistoricalRunCount { get; set; }
    public int CompletedRunCount { get; set; }
    public int AcceptedCandidateCount { get; set; }
    public int DismissedCandidateCount { get; set; }
    public int SupersededCandidateCount { get; set; }
    public int ArchivedCandidateCount { get; set; }
    public int RunsWithAcceptedCandidates { get; set; }
    public int RunsWithoutAcceptedCandidates { get; set; }
    public DateTime? LastCompletedUtc { get; set; }
    public DateTime? LastAcceptedUtc { get; set; }
}