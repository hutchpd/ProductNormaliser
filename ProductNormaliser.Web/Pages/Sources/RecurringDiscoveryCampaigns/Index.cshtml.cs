using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Models;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Pages.Sources.RecurringDiscoveryCampaigns;

public sealed class IndexModel(
    IProductNormaliserAdminApiClient adminApiClient,
    ILogger<IndexModel> logger) : PageModel
{
    private const string OperatorAssistedMode = "operator_assisted";
    private const string SuggestAcceptMode = "suggest_accept";
    private const string AutoAcceptAndSeedMode = "auto_accept_and_seed";

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public CreateCampaignInput Campaign { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public IReadOnlyList<CategoryMetadataDto> Categories { get; private set; } = [];

    public IReadOnlyList<RecurringDiscoveryCampaignDto> Campaigns { get; private set; } = [];

    public int ActiveCampaignCount => Campaigns.Count(campaign => string.Equals(campaign.Status, "active", StringComparison.OrdinalIgnoreCase));

    public int PausedCampaignCount => Campaigns.Count(campaign => string.Equals(campaign.Status, "paused", StringComparison.OrdinalIgnoreCase));

    public int CampaignsWithAcceptedMemory => Campaigns.Count(campaign => campaign.AcceptedCandidateCount > 0);

    public PageHeroModel Hero => new()
    {
        Eyebrow = "Continuous Discovery",
        Title = "Manage recurring discovery campaigns",
        Description = "Create repeatable discovery schedules keyed by category, market, locale, and brand hints. Maintenance will queue fresh runs while preserving longitudinal learning from prior outcomes.",
        Metrics =
        [
            new HeroMetricModel { Label = "Campaigns", Value = Campaigns.Count.ToString() },
            new HeroMetricModel { Label = "Active", Value = ActiveCampaignCount.ToString() },
            new HeroMetricModel { Label = "Paused", Value = PausedCampaignCount.ToString() },
            new HeroMetricModel { Label = "With accepts", Value = CampaignsWithAcceptedMemory.ToString() }
        ]
    };

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        if (!TryValidateCreateCampaign())
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        try
        {
            var campaign = await adminApiClient.CreateRecurringDiscoveryCampaignAsync(new CreateRecurringDiscoveryCampaignRequest
            {
                Name = NormalizeOptionalText(Campaign.Name),
                CategoryKeys = NormalizeValues(Campaign.CategoryKeys),
                Locale = NormalizeOptionalText(Campaign.Locale),
                Market = NormalizeOptionalText(Campaign.Market),
                AutomationMode = Campaign.AutomationMode,
                BrandHints = ParseDelimitedValues(Campaign.BrandHints),
                MaxCandidatesPerRun = Campaign.MaxCandidatesPerRun,
                IntervalHours = Campaign.IntervalHours
            }, cancellationToken);

            StatusMessage = $"Created recurring discovery campaign '{campaign.Name}'. Maintenance will queue the next due run automatically.";
            return RedirectToPage();
        }
        catch (AdminApiValidationException exception)
        {
            foreach (var entry in exception.Errors)
            {
                foreach (var message in entry.Value)
                {
                    ModelState.AddModelError(string.Empty, message);
                }
            }

            await LoadAsync(cancellationToken);
            return Page();
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to create recurring discovery campaign.");
            ErrorMessage = exception.Message;
            await LoadAsync(cancellationToken);
            return Page();
        }
    }

    public Task<IActionResult> OnPostPauseAsync(string campaignId, CancellationToken cancellationToken)
    {
        return MutateCampaignAsync(
            campaignId,
            () => adminApiClient.PauseRecurringDiscoveryCampaignAsync(campaignId, cancellationToken),
            campaign => $"Paused recurring discovery campaign '{campaign.Name}'.");
    }

    public Task<IActionResult> OnPostResumeAsync(string campaignId, CancellationToken cancellationToken)
    {
        return MutateCampaignAsync(
            campaignId,
            () => adminApiClient.ResumeRecurringDiscoveryCampaignAsync(campaignId, cancellationToken),
            campaign => $"Resumed recurring discovery campaign '{campaign.Name}'.");
    }

    public async Task<IActionResult> OnPostDeleteAsync(string campaignId, CancellationToken cancellationToken)
    {
        try
        {
            await adminApiClient.DeleteRecurringDiscoveryCampaignAsync(campaignId, cancellationToken);
            StatusMessage = $"Deleted recurring discovery campaign '{campaignId}'. Historical discovery runs were preserved.";
            return RedirectToPage();
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to delete recurring discovery campaign {CampaignId}.", campaignId);
            ErrorMessage = exception.Message;
            await LoadAsync(cancellationToken);
            return Page();
        }
    }

    public string GetAutomationModeLabel(string? automationMode)
    {
        return automationMode?.Trim().ToLowerInvariant() switch
        {
            SuggestAcceptMode => "Suggest accept",
            AutoAcceptAndSeedMode => "Auto-accept and seed",
            _ => "Operator-assisted"
        };
    }

    public string GetScopeSummary(RecurringDiscoveryCampaignDto campaign)
    {
        var segments = new List<string>
        {
            string.Join(", ", campaign.CategoryKeys)
        };

        if (!string.IsNullOrWhiteSpace(campaign.Market))
        {
            segments.Add($"market {campaign.Market}");
        }

        if (!string.IsNullOrWhiteSpace(campaign.Locale))
        {
            segments.Add($"locale {campaign.Locale}");
        }

        if (campaign.BrandHints.Count > 0)
        {
            segments.Add($"brands {string.Join(", ", campaign.BrandHints)}");
        }

        return string.Join(" | ", segments);
    }

    public string GetScheduleSummary(RecurringDiscoveryCampaignDto campaign)
    {
        var parts = new List<string>
        {
            $"Every {campaign.IntervalHours} hour{(campaign.IntervalHours == 1 ? string.Empty : "s")}",
            $"max {campaign.MaxCandidatesPerRun} candidates/run"
        };

        if (campaign.NextScheduledUtc is not null)
        {
            parts.Add($"next {campaign.NextScheduledUtc.Value:u}");
        }

        if (campaign.LastScheduledUtc is not null)
        {
            parts.Add($"last scheduled {campaign.LastScheduledUtc.Value:u}");
        }

        return string.Join(" | ", parts);
    }

    public string GetMemorySummary(RecurringDiscoveryCampaignDto campaign)
    {
        if (campaign.HistoricalRunCount == 0)
        {
            return "No historical runs yet. The first due maintenance sweep will queue the initial run.";
        }

        return $"{campaign.HistoricalRunCount} run(s), {campaign.AcceptedCandidateCount} accepted, {campaign.DismissedCandidateCount} dismissed, {campaign.SupersededCandidateCount} superseded, {campaign.RunsWithAcceptedCandidates} run(s) with accepted candidates.";
    }

    public StatusBadgeModel GetStatusBadge(RecurringDiscoveryCampaignDto campaign)
    {
        return string.Equals(campaign.Status, "paused", StringComparison.OrdinalIgnoreCase)
            ? new StatusBadgeModel { Text = "Paused", Tone = "warning" }
            : new StatusBadgeModel { Text = "Active", Tone = "success" };
    }

    private async Task<IActionResult> MutateCampaignAsync(string campaignId, Func<Task<RecurringDiscoveryCampaignDto>> mutation, Func<RecurringDiscoveryCampaignDto, string> buildStatusMessage)
    {
        try
        {
            var campaign = await mutation();
            StatusMessage = buildStatusMessage(campaign);
            return RedirectToPage();
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to mutate recurring discovery campaign {CampaignId}.", campaignId);
            ErrorMessage = exception.Message;
            await LoadAsync(HttpContext.RequestAborted);
            return Page();
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var categoriesTask = adminApiClient.GetCategoriesAsync(cancellationToken);
        var campaignsTask = adminApiClient.GetRecurringDiscoveryCampaignsAsync(cancellationToken: cancellationToken);
        await Task.WhenAll(categoriesTask, campaignsTask);

        Categories = categoriesTask.Result
            .OrderBy(category => category.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Campaigns = campaignsTask.Result
            .OrderBy(campaign => string.Equals(campaign.Status, "paused", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(campaign => campaign.NextScheduledUtc ?? DateTime.MaxValue)
            .ThenByDescending(campaign => campaign.UpdatedUtc)
            .ToArray();
    }

    private bool TryValidateCreateCampaign()
    {
        if (!ModelState.IsValid)
        {
            return false;
        }

        var categoryKeys = NormalizeValues(Campaign.CategoryKeys);
        if (categoryKeys.Count == 0)
        {
            ModelState.AddModelError($"{nameof(Campaign)}.{nameof(Campaign.CategoryKeys)}", "Select at least one category.");
        }

        return ModelState.IsValid;
    }

    private static List<string> NormalizeValues(IEnumerable<string>? values)
    {
        return (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ParseDelimitedValues(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return NormalizeValues(value.Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public sealed class CreateCampaignInput
    {
        [Display(Name = "Name")]
        [StringLength(120)]
        public string? Name { get; set; }

        [Display(Name = "Categories")]
        public List<string> CategoryKeys { get; set; } = [];

        [Display(Name = "Locale")]
        [StringLength(32)]
        public string? Locale { get; set; }

        [Display(Name = "Market")]
        [StringLength(32)]
        public string? Market { get; set; }

        [Display(Name = "Automation mode")]
        [Required]
        public string AutomationMode { get; set; } = OperatorAssistedMode;

        [Display(Name = "Brand hints")]
        [StringLength(400)]
        public string? BrandHints { get; set; }

        [Display(Name = "Max candidates per run")]
        [Range(1, 25)]
        public int MaxCandidatesPerRun { get; set; } = 10;

        [Display(Name = "Interval hours")]
        [Range(1, 168)]
        public int? IntervalHours { get; set; } = 24;
    }
}