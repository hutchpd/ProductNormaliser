namespace ProductNormaliser.AdminApi.Contracts;

public sealed class RecurringDiscoveryCampaignDto
{
    public string CampaignId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> CategoryKeys { get; init; } = [];
    public string? Locale { get; init; }
    public string? Market { get; init; }
    public IReadOnlyList<string> BrandHints { get; init; } = [];
    public string AutomationMode { get; init; } = string.Empty;
    public int MaxCandidatesPerRun { get; init; }
    public int IntervalHours { get; init; }
    public string Status { get; init; } = string.Empty;
    public string CampaignFingerprint { get; init; } = string.Empty;
    public string? LastRunId { get; init; }
    public string? StatusMessage { get; init; }
    public int HistoricalRunCount { get; init; }
    public int CompletedRunCount { get; init; }
    public int AcceptedCandidateCount { get; init; }
    public int DismissedCandidateCount { get; init; }
    public int SupersededCandidateCount { get; init; }
    public int ArchivedCandidateCount { get; init; }
    public int RunsWithAcceptedCandidates { get; init; }
    public int RunsWithoutAcceptedCandidates { get; init; }
    public DateTime? LastCompletedUtc { get; init; }
    public DateTime? LastAcceptedUtc { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime UpdatedUtc { get; init; }
    public DateTime? LastScheduledUtc { get; init; }
    public DateTime? NextScheduledUtc { get; init; }
}