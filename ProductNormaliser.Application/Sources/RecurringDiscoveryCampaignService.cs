using ProductNormaliser.Application.Categories;
using ProductNormaliser.Application.Governance;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Sources;

public sealed class RecurringDiscoveryCampaignService(
    IDiscoveryCampaignStore discoveryCampaignStore,
    ICategoryMetadataService categoryMetadataService,
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

        var intervalHours = NormalizeIntervalHours(request.IntervalHours);
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
            IntervalHours = intervalHours,
            Status = RecurringDiscoveryCampaignStatuses.Active,
            CampaignFingerprint = fingerprint,
            StatusMessage = "Recurring discovery campaign is active and waiting for the next maintenance sweep.",
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow,
            NextScheduledUtc = utcNow
        };

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
                ["intervalHours"] = campaign.IntervalHours.ToString(System.Globalization.CultureInfo.InvariantCulture)
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
        campaign.StatusMessage = "Recurring discovery campaign resumed and is eligible for the next maintenance sweep.";
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

    private int NormalizeIntervalHours(int? intervalHours)
    {
        var value = intervalHours ?? options.RecurringCampaignDefaultIntervalHours;
        return Math.Clamp(value, options.RecurringCampaignMinIntervalHours, options.RecurringCampaignMaxIntervalHours);
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