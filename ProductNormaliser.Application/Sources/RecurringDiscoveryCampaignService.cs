using ProductNormaliser.Application.Categories;
using ProductNormaliser.Application.Governance;
using ProductNormaliser.Core.Models;
using System.Globalization;

namespace ProductNormaliser.Application.Sources;

public sealed class RecurringDiscoveryCampaignService(
    IDiscoveryCampaignStore discoveryCampaignStore,
    ICategoryMetadataService categoryMetadataService,
    IDiscoveryRunService discoveryRunService,
    IManagementAuditService managementAuditService,
    Microsoft.Extensions.Options.IOptions<DiscoveryRunOperationsOptions> options) : IRecurringDiscoveryCampaignService
{
    private readonly DiscoveryRunOperationsOptions options = options.Value;

    public async Task<IReadOnlyList<RecurringDiscoveryCampaign>> ListAsync(string? status = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return await discoveryCampaignStore.ListAsync(cancellationToken);
        }

        return await discoveryCampaignStore.ListByStatusesAsync([NormalizeStatus(status)], cancellationToken);
    }

    public Task<RecurringDiscoveryCampaign?> GetAsync(string campaignId, CancellationToken cancellationToken = default)
    {
        return discoveryCampaignStore.GetAsync(NormalizeRequired(campaignId, nameof(campaignId)), cancellationToken);
    }

    public async Task<RecurringDiscoveryCampaign> CreateAsync(CreateRecurringDiscoveryCampaignRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var categoryKeys = SourceCandidateDiscoveryEvaluator.NormalizeValues(request.CategoryKeys);
        if (categoryKeys.Count == 0)
        {
            throw new ArgumentException("Choose at least one category before creating a recurring discovery campaign.", nameof(request));
        }

        var knownCategoryKeys = (await categoryMetadataService.ListAsync(enabledOnly: false, cancellationToken))
            .Select(category => category.CategoryKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknownCategoryKeys = categoryKeys
            .Where(categoryKey => !knownCategoryKeys.Contains(categoryKey))
            .ToArray();
        if (unknownCategoryKeys.Length > 0)
        {
            throw new ArgumentException($"Unknown category keys: {string.Join(", ", unknownCategoryKeys)}.", nameof(request));
        }

        var normalizedMarket = SourceCandidateDiscoveryEvaluator.NormalizeOptionalText(request.Market);
        var normalizedLocale = SourceCandidateDiscoveryEvaluator.NormalizeOptionalText(request.Locale);
        var brandHints = SourceCandidateDiscoveryEvaluator.NormalizeValues(request.BrandHints);
        var fingerprint = DiscoveryRunScopePolicy.CreateFingerprint(normalizedMarket, normalizedLocale, categoryKeys, brandHints);
        var existingCampaign = await discoveryCampaignStore.GetByFingerprintAsync(fingerprint, cancellationToken);
        if (existingCampaign is not null)
        {
            throw new InvalidOperationException($"A recurring discovery campaign already exists for scope '{existingCampaign.Name}'.");
        }

        var intervalMinutes = NormalizeIntervalMinutes(request.IntervalMinutes);
        var utcNow = DateTime.UtcNow;
        var campaign = new RecurringDiscoveryCampaign
        {
            CampaignId = $"discovery_campaign_{Guid.NewGuid():N}",
            Name = BuildName(request.Name, categoryKeys, normalizedMarket, normalizedLocale, brandHints),
            CategoryKeys = categoryKeys,
            Market = normalizedMarket,
            Locale = normalizedLocale,
            AutomationMode = SourceAutomationModes.Normalize(request.AutomationMode),
            BrandHints = brandHints,
            MaxCandidatesPerRun = SourceCandidateDiscoveryEvaluator.NormalizeMaxCandidates(request.MaxCandidatesPerRun),
            IntervalMinutes = intervalMinutes,
            IntervalHours = 0,
            Status = RecurringDiscoveryCampaignStatuses.Active,
            CampaignFingerprint = fingerprint,
            StatusMessage = "Recurring discovery campaign is active. Queueing the initial run now.",
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow,
            NextScheduledUtc = utcNow.AddMinutes(intervalMinutes)
        };

        await discoveryCampaignStore.UpsertAsync(campaign, cancellationToken);
        var scheduledRun = await discoveryRunService.CreateScheduledAsync(campaign, cancellationToken);
        campaign.LastRunId = scheduledRun.RunId;
        campaign.LastScheduledUtc = utcNow;
        campaign.StatusMessage = $"Recurring discovery campaign queued initial run '{scheduledRun.RunId}'. Future runs will be scheduled automatically.";
        campaign.UpdatedUtc = DateTime.UtcNow;
        await discoveryCampaignStore.UpsertAsync(campaign, cancellationToken);

        await managementAuditService.RecordAsync(
            "recurring_discovery_campaign_created",
            "recurring_discovery_campaign",
            campaign.CampaignId,
            new Dictionary<string, string>
            {
                ["categories"] = string.Join(',', campaign.CategoryKeys),
                ["market"] = campaign.Market ?? string.Empty,
                ["locale"] = campaign.Locale ?? string.Empty,
                ["automationMode"] = campaign.AutomationMode,
                ["intervalMinutes"] = campaign.IntervalMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture)
            },
            cancellationToken);

        return campaign;
    }

    public async Task<RecurringDiscoveryCampaign?> UpdateConfigurationAsync(string campaignId, int? intervalMinutes, int? maxCandidatesPerRun, CancellationToken cancellationToken = default)
    {
        var campaign = await discoveryCampaignStore.GetAsync(NormalizeRequired(campaignId, nameof(campaignId)), cancellationToken);
        if (campaign is null)
        {
            return null;
        }

        var currentIntervalMinutes = campaign.ResolveIntervalMinutes();
        var normalizedIntervalMinutes = intervalMinutes.HasValue
            ? NormalizeIntervalMinutes(intervalMinutes.Value)
            : currentIntervalMinutes;
        var normalizedMaxCandidatesPerRun = maxCandidatesPerRun.HasValue
            ? SourceCandidateDiscoveryEvaluator.NormalizeMaxCandidates(maxCandidatesPerRun.Value)
            : campaign.MaxCandidatesPerRun;
        var utcNow = DateTime.UtcNow;

        campaign.IntervalMinutes = normalizedIntervalMinutes;
        campaign.IntervalHours = 0;
        campaign.MaxCandidatesPerRun = normalizedMaxCandidatesPerRun;
        if (normalizedIntervalMinutes != currentIntervalMinutes)
        {
            campaign.NextScheduledUtc = utcNow.AddMinutes(normalizedIntervalMinutes);
        }

        campaign.UpdatedUtc = utcNow;
        campaign.StatusMessage = string.Equals(campaign.Status, RecurringDiscoveryCampaignStatuses.Paused, StringComparison.OrdinalIgnoreCase)
            ? $"Recurring discovery campaign updated to every {DescribeInterval(normalizedIntervalMinutes)} with a cap of {normalizedMaxCandidatesPerRun} candidates per run. Runs remain paused until the campaign is resumed."
            : normalizedIntervalMinutes != currentIntervalMinutes
                ? $"Recurring discovery campaign updated to every {DescribeInterval(normalizedIntervalMinutes)} with a cap of {normalizedMaxCandidatesPerRun} candidates per run. The next run will follow the new schedule window."
                : $"Recurring discovery campaign updated to every {DescribeInterval(normalizedIntervalMinutes)} with a cap of {normalizedMaxCandidatesPerRun} candidates per run.";

        await discoveryCampaignStore.UpsertAsync(campaign, cancellationToken);
        await managementAuditService.RecordAsync(
            "recurring_discovery_campaign_configuration_updated",
            "recurring_discovery_campaign",
            campaign.CampaignId,
            new Dictionary<string, string>
            {
                ["intervalMinutes"] = normalizedIntervalMinutes.ToString(CultureInfo.InvariantCulture),
                ["maxCandidatesPerRun"] = normalizedMaxCandidatesPerRun.ToString(CultureInfo.InvariantCulture),
                ["status"] = campaign.Status
            },
            cancellationToken);

        return campaign;
    }

    public async Task<RecurringDiscoveryCampaign?> PauseAsync(string campaignId, CancellationToken cancellationToken = default)
    {
        var campaign = await discoveryCampaignStore.GetAsync(NormalizeRequired(campaignId, nameof(campaignId)), cancellationToken);
        if (campaign is null)
        {
            return null;
        }

        if (string.Equals(campaign.Status, RecurringDiscoveryCampaignStatuses.Paused, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Recurring discovery campaign '{campaign.CampaignId}' is already paused.");
        }

        campaign.Status = RecurringDiscoveryCampaignStatuses.Paused;
        campaign.StatusMessage = "Recurring discovery campaign is paused and will not schedule new runs.";
        campaign.UpdatedUtc = DateTime.UtcNow;
        await discoveryCampaignStore.UpsertAsync(campaign, cancellationToken);
        return campaign;
    }

    public async Task<RecurringDiscoveryCampaign?> ResumeAsync(string campaignId, CancellationToken cancellationToken = default)
    {
        var campaign = await discoveryCampaignStore.GetAsync(NormalizeRequired(campaignId, nameof(campaignId)), cancellationToken);
        if (campaign is null)
        {
            return null;
        }

        if (string.Equals(campaign.Status, RecurringDiscoveryCampaignStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Recurring discovery campaign '{campaign.CampaignId}' is already active.");
        }

        campaign.Status = RecurringDiscoveryCampaignStatuses.Active;
        campaign.StatusMessage = "Recurring discovery campaign resumed. Future runs will be scheduled automatically when the next window opens.";
        campaign.NextScheduledUtc ??= DateTime.UtcNow;
        if (campaign.NextScheduledUtc < DateTime.UtcNow)
        {
            campaign.NextScheduledUtc = DateTime.UtcNow;
        }

        campaign.UpdatedUtc = DateTime.UtcNow;
        await discoveryCampaignStore.UpsertAsync(campaign, cancellationToken);
        return campaign;
    }

    public async Task<bool> DeleteAsync(string campaignId, CancellationToken cancellationToken = default)
    {
        var normalizedCampaignId = NormalizeRequired(campaignId, nameof(campaignId));
        var deleted = await discoveryCampaignStore.DeleteAsync(normalizedCampaignId, cancellationToken);
        if (!deleted)
        {
            return false;
        }

        await managementAuditService.RecordAsync(
            "recurring_discovery_campaign_deleted",
            "recurring_discovery_campaign",
            normalizedCampaignId,
            new Dictionary<string, string>
            {
                ["preservedRunHistory"] = "true"
            },
            cancellationToken);

        return true;
    }

    private int NormalizeIntervalMinutes(int? intervalMinutes)
    {
        var defaultMinutes = options.RecurringCampaignDefaultIntervalMinutes > 0
            ? options.RecurringCampaignDefaultIntervalMinutes
            : checked(options.RecurringCampaignDefaultIntervalHours * 60);
        var minMinutes = options.RecurringCampaignMinIntervalMinutes > 0
            ? options.RecurringCampaignMinIntervalMinutes
            : checked(options.RecurringCampaignMinIntervalHours * 60);
        var maxMinutes = options.RecurringCampaignMaxIntervalMinutes > 0
            ? options.RecurringCampaignMaxIntervalMinutes
            : checked(options.RecurringCampaignMaxIntervalHours * 60);
        var value = intervalMinutes ?? defaultMinutes;
        return Math.Clamp(value, minMinutes, maxMinutes);
    }

    private static string DescribeInterval(int intervalMinutes)
    {
        if (intervalMinutes < 60)
        {
            return $"{intervalMinutes} minute{(intervalMinutes == 1 ? string.Empty : "s")}";
        }

        var hours = intervalMinutes / 60;
        var minutes = intervalMinutes % 60;
        if (minutes == 0)
        {
            return $"{hours} hour{(hours == 1 ? string.Empty : "s")}";
        }

        return $"{hours} hour{(hours == 1 ? string.Empty : "s")} {minutes} minute{(minutes == 1 ? string.Empty : "s")}";
    }

    private static string BuildName(string? name, IReadOnlyList<string> categoryKeys, string? market, string? locale, IReadOnlyList<string> brandHints)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Trim();
        }

        var categorySummary = string.Join(", ", categoryKeys);
        var scope = string.Join(" / ", new[] { market, locale }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var brands = brandHints.Count == 0 ? string.Empty : $" [{string.Join(", ", brandHints)}]";
        return string.IsNullOrWhiteSpace(scope)
            ? $"{categorySummary}{brands}"
            : $"{categorySummary} ({scope}){brands}";
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value cannot be empty.", parameterName)
            : value.Trim();
    }

    private static string NormalizeStatus(string value)
    {
        return string.Equals(value?.Trim(), RecurringDiscoveryCampaignStatuses.Paused, StringComparison.OrdinalIgnoreCase)
            ? RecurringDiscoveryCampaignStatuses.Paused
            : RecurringDiscoveryCampaignStatuses.Active;
    }
}