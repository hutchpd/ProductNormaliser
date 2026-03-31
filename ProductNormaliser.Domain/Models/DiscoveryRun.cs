namespace ProductNormaliser.Core.Models;

public sealed class DiscoveryRun
{
    public string RunId { get; set; } = default!;
    public string TriggerKind { get; set; } = DiscoveryRunTriggerKinds.Manual;
    public string? RecurringCampaignId { get; set; }
    public string? RecurringCampaignFingerprint { get; set; }
    public IReadOnlyList<string> RequestedCategoryKeys { get; set; } = [];
    public string? Locale { get; set; }
    public string? Market { get; set; }
    public string AutomationMode { get; set; } = SourceAutomationModes.OperatorAssisted;
    public IReadOnlyList<string> BrandHints { get; set; } = [];
    public int MaxCandidates { get; set; } = 10;
    public string Status { get; set; } = DiscoveryRunStatuses.Queued;
    public string CurrentStage { get; set; } = DiscoveryRunStageNames.Search;
    public string? StatusMessage { get; set; }
    public string? FailureMessage { get; set; }
    public string LlmStatus { get; set; } = "disabled";
    public string LlmStatusMessage { get; set; } = string.Empty;
    public int SearchResultCount { get; set; }
    public int CollapsedCandidateCount { get; set; }
    public int ProbeCompletedCount { get; set; }
    public long ProbeTotalElapsedMs { get; set; }
    public long? ProbeAverageElapsedMs { get; set; }
    public int LlmQueueDepth { get; set; }
    public int LlmCompletedCount { get; set; }
    public long LlmTotalElapsedMs { get; set; }
    public long? LlmAverageElapsedMs { get; set; }
    public long? SearchElapsedMs { get; set; }
    public long? SearchTimeoutBudgetMs { get; set; }
    public long? ProbeTimeoutBudgetMs { get; set; }
    public long? LlmTimeoutBudgetMs { get; set; }
    public int SuggestedCandidateCount { get; set; }
    public int AutoAcceptedCandidateCount { get; set; }
    public int PublishedCandidateCount { get; set; }
    public decimal? CandidateThroughputPerMinute { get; set; }
    public decimal? AcceptanceRate { get; set; }
    public decimal? ManualReviewRate { get; set; }
    public long? TimeToFirstAcceptedCandidateMs { get; set; }
    public DateTime? FirstAcceptedUtc { get; set; }
    public int RecoveryAttemptCount { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public DateTime? CancelRequestedUtc { get; set; }
    public DateTime? LastHeartbeatUtc { get; set; }
    public List<DiscoveryRunDiagnostic> Diagnostics { get; set; } = [];
}