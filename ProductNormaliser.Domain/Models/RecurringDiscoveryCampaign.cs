namespace ProductNormaliser.Core.Models;

public sealed class RecurringDiscoveryCampaign
{
    public string CampaignId { get; set; } = default!;
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<string> CategoryKeys { get; set; } = [];
    public string? Locale { get; set; }
    public string? Market { get; set; }
    public IReadOnlyList<string> BrandHints { get; set; } = [];
    public string AutomationMode { get; set; } = SourceAutomationModes.OperatorAssisted;
    public int MaxCandidatesPerRun { get; set; } = 10;
    public int IntervalHours { get; set; } = 24;
    public string Status { get; set; } = RecurringDiscoveryCampaignStatuses.Active;
    public string CampaignFingerprint { get; set; } = string.Empty;
    public string? LastRunId { get; set; }
    public string? StatusMessage { get; set; }
    public RecurringDiscoveryCampaignMemory Memory { get; set; } = new();
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? LastScheduledUtc { get; set; }
    public DateTime? NextScheduledUtc { get; set; }
}