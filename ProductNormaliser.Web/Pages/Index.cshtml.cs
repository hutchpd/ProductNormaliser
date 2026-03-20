using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Pages;

public sealed class IndexModel(
    IProductNormaliserAdminApiClient adminApiClient,
    ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "category")]
    public string? SelectedCategoryKey { get; set; }

    [BindProperty]
    public RegisterSourceInput RegisterSource { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public IReadOnlyList<CategoryMetadataDto> Categories { get; private set; } = [];

    public CategoryDetailDto? SelectedCategory { get; private set; }

    public IReadOnlyList<SourceDto> Sources { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadDashboardAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostRegisterSourceAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadDashboardAsync(cancellationToken);
            return Page();
        }

        try
        {
            await adminApiClient.RegisterSourceAsync(new RegisterSourceRequest
            {
                SourceId = RegisterSource.SourceId,
                DisplayName = RegisterSource.DisplayName,
                BaseUrl = RegisterSource.BaseUrl,
                Description = RegisterSource.Description,
                IsEnabled = RegisterSource.IsEnabled,
                SupportedCategoryKeys = RegisterSource.SelectedCategoryKeys,
                ThrottlingPolicy = new SourceThrottlingPolicyDto
                {
                    MinDelayMs = RegisterSource.MinDelayMs,
                    MaxDelayMs = RegisterSource.MaxDelayMs,
                    MaxConcurrentRequests = RegisterSource.MaxConcurrentRequests,
                    RequestsPerMinute = RegisterSource.RequestsPerMinute,
                    RespectRobotsTxt = RegisterSource.RespectRobotsTxt
                }
            }, cancellationToken);

            StatusMessage = $"Registered source '{RegisterSource.DisplayName}'.";
            return RedirectToPage(new { category = SelectedCategoryKey });
        }
        catch (AdminApiValidationException exception)
        {
            AddValidationErrors(exception);
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to register source from the dashboard.");
            ErrorMessage = exception.Message;
        }

        await LoadDashboardAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostToggleSourceAsync(string sourceId, bool currentlyEnabled, string? category, CancellationToken cancellationToken)
    {
        SelectedCategoryKey = category;

        try
        {
            if (currentlyEnabled)
            {
                await adminApiClient.DisableSourceAsync(sourceId, cancellationToken);
                StatusMessage = $"Disabled source '{sourceId}'.";
            }
            else
            {
                await adminApiClient.EnableSourceAsync(sourceId, cancellationToken);
                StatusMessage = $"Enabled source '{sourceId}'.";
            }

            return RedirectToPage(new { category });
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to toggle source {SourceId} from dashboard.", sourceId);
            ErrorMessage = exception.Message;
            await LoadDashboardAsync(cancellationToken);
            return Page();
        }
    }

    private async Task LoadDashboardAsync(CancellationToken cancellationToken)
    {
        try
        {
            Categories = await adminApiClient.GetEnabledCategoriesAsync(cancellationToken);
            Sources = await adminApiClient.GetSourcesAsync(cancellationToken);

            var effectiveCategoryKey = SelectedCategoryKey;
            if (string.IsNullOrWhiteSpace(effectiveCategoryKey))
            {
                effectiveCategoryKey = Categories.FirstOrDefault()?.CategoryKey;
            }

            if (!string.IsNullOrWhiteSpace(effectiveCategoryKey))
            {
                SelectedCategory = await adminApiClient.GetCategoryDetailAsync(effectiveCategoryKey, cancellationToken);
                SelectedCategoryKey = SelectedCategory?.Metadata.CategoryKey ?? effectiveCategoryKey;
            }

            if (RegisterSource.SelectedCategoryKeys.Count == 0 && !string.IsNullOrWhiteSpace(SelectedCategoryKey))
            {
                RegisterSource.SelectedCategoryKeys = [SelectedCategoryKey];
            }
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to load dashboard data from Admin API.");
            ErrorMessage = exception.Message;
            Categories = [];
            Sources = [];
            SelectedCategory = null;
        }
    }

    private void AddValidationErrors(AdminApiValidationException exception)
    {
        foreach (var entry in exception.Errors)
        {
            var field = entry.Key switch
            {
                "sourceId" => nameof(RegisterSource.SourceId),
                "displayName" => nameof(RegisterSource.DisplayName),
                "baseUrl" => nameof(RegisterSource.BaseUrl),
                "supportedCategoryKeys" or "categoryKeys" => nameof(RegisterSource.SelectedCategoryKeys),
                "policy" => string.Empty,
                _ => string.Empty
            };

            foreach (var message in entry.Value)
            {
                ModelState.AddModelError(string.IsNullOrWhiteSpace(field) ? string.Empty : $"{nameof(RegisterSource)}.{field}", message);
            }
        }
    }

    public sealed class RegisterSourceInput
    {
        [Required]
        public string SourceId { get; set; } = string.Empty;

        [Required]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [Url]
        public string BaseUrl { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsEnabled { get; set; } = true;

        public List<string> SelectedCategoryKeys { get; set; } = [];

        [Range(0, int.MaxValue)]
        public int MinDelayMs { get; set; } = 1000;

        [Range(0, int.MaxValue)]
        public int MaxDelayMs { get; set; } = 3500;

        [Range(1, int.MaxValue)]
        public int MaxConcurrentRequests { get; set; } = 2;

        [Range(1, int.MaxValue)]
        public int RequestsPerMinute { get; set; } = 30;

        public bool RespectRobotsTxt { get; set; } = true;

    }
}
