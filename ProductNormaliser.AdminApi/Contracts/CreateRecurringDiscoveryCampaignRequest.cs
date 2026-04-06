namespace ProductNormaliser.AdminApi.Contracts;

public sealed class CreateRecurringDiscoveryCampaignRequest
{
    public string? Name { get; init; }
    public IReadOnlyList<string> CategoryKeys { get; init; } = [];
    public string? Locale { get; init; }
    public string? Market { get; init; }
    public string? AutomationMode { get; init; }
    public IReadOnlyList<string> BrandHints { get; init; } = [];
    public int MaxCandidatesPerRun { get; init; } = 10;
    public int? IntervalMinutes { get; init; }
}