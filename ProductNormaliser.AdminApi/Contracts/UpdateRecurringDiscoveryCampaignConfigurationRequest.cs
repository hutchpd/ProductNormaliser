namespace ProductNormaliser.AdminApi.Contracts;

public sealed class UpdateRecurringDiscoveryCampaignConfigurationRequest
{
    public int? IntervalMinutes { get; init; }
    public int? MaxCandidatesPerRun { get; init; }
}