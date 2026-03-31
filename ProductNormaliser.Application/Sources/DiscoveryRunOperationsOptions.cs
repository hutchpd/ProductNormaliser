namespace ProductNormaliser.Application.Sources;

public sealed class DiscoveryRunOperationsOptions
{
    public const string SectionName = "DiscoveryRunOperations";

    public int SearchTimeoutSeconds { get; set; } = 20;
    public int ProbeTimeoutSeconds { get; set; } = 12;
    public int LlmVerificationTimeoutMs { get; set; } = 15000;
    public int MaintenanceSweepIntervalSeconds { get; set; } = 30;
    public int CandidateArchiveRetentionHours { get; set; } = 24;
    public int AbandonedHeartbeatTimeoutMinutes { get; set; } = 10;
    public int MaxRecoveryAttempts { get; set; } = 1;
    public int ExpandedRunCount { get; set; } = 3;
    public int RecurringCampaignDefaultIntervalHours { get; set; } = 24;
    public int RecurringCampaignMinIntervalHours { get; set; } = 6;
    public int RecurringCampaignMaxIntervalHours { get; set; } = 168;
    public int RecurringCampaignSweepBatchSize { get; set; } = 10;
}