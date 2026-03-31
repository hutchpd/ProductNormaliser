namespace ProductNormaliser.AdminApi.Contracts;

public sealed class DiscoveryRunDto
{
    public string RunId { get; init; } = string.Empty;
    public string TriggerKind { get; init; } = "manual";
    public string? RecurringCampaignId { get; init; }
    public IReadOnlyList<string> RequestedCategoryKeys { get; init; } = [];
    public string? Locale { get; init; }
    public string? Market { get; init; }
    public string AutomationMode { get; init; } = string.Empty;
    public IReadOnlyList<string> BrandHints { get; init; } = [];
    public int MaxCandidates { get; init; }
    public string Status { get; init; } = string.Empty;
    public string CurrentStage { get; init; } = string.Empty;
    public string? StatusMessage { get; init; }
    public string? FailureMessage { get; init; }
    public string LlmStatus { get; init; } = string.Empty;
    public string LlmStatusMessage { get; init; } = string.Empty;
    public int SearchResultCount { get; init; }
    public int CollapsedCandidateCount { get; init; }
    public int ProbeCompletedCount { get; init; }
    public long ProbeTotalElapsedMs { get; init; }
    public long? ProbeAverageElapsedMs { get; init; }
    public int LlmQueueDepth { get; init; }
    public int LlmCompletedCount { get; init; }
    public long LlmTotalElapsedMs { get; init; }
    public long? LlmAverageElapsedMs { get; init; }
    public long? SearchElapsedMs { get; init; }
    public long? SearchTimeoutBudgetMs { get; init; }
    public long? ProbeTimeoutBudgetMs { get; init; }
    public long? LlmTimeoutBudgetMs { get; init; }
    public int SuggestedCandidateCount { get; init; }
    public int AutoAcceptedCandidateCount { get; init; }
    public int PublishedCandidateCount { get; init; }
    public decimal? CandidateThroughputPerMinute { get; init; }
    public decimal? AcceptanceRate { get; init; }
    public decimal? ManualReviewRate { get; init; }
    public long? TimeToFirstAcceptedCandidateMs { get; init; }
    public DateTime? FirstAcceptedUtc { get; init; }
    public int RecoveryAttemptCount { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime UpdatedUtc { get; init; }
    public DateTime? StartedUtc { get; init; }
    public DateTime? CompletedUtc { get; init; }
    public DateTime? CancelRequestedUtc { get; init; }
    public IReadOnlyList<SourceCandidateDiscoveryDiagnosticDto> Diagnostics { get; init; } = [];
}