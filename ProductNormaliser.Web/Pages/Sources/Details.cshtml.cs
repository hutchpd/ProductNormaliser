using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Models;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Pages.Sources;

public sealed class DetailsModel(
    IProductNormaliserAdminApiClient adminApiClient,
    ILogger<DetailsModel> logger) : PageModel
{
    [BindProperty]
    public EditSourceInput Source { get; set; } = new();

    [BindProperty]
    public CategoryAssignmentInput CategoriesForm { get; set; } = new();

    [BindProperty]
    public ThrottlingInput Throttling { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public SourceDto? CurrentSource { get; private set; }

    public IReadOnlyList<CategoryMetadataDto> AvailableCategories { get; private set; } = [];

    public IReadOnlyDictionary<string, CategoryMetadataDto> CategoryLookup
        => AvailableCategories.ToDictionary(category => category.CategoryKey, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> AssignedCategoryLabels => CurrentSource is null
        ? []
        : CurrentSource.SupportedCategoryKeys
            .Select(categoryKey => CategoryLookup.TryGetValue(categoryKey, out var category) ? category.DisplayName : categoryKey)
            .ToArray();

    public string IntelligenceUrl => CurrentSource is null || CurrentSource.SupportedCategoryKeys.Count == 0
        ? "/Sources/Intelligence"
        : $"/Sources/Intelligence?category={Uri.EscapeDataString(CurrentSource.SupportedCategoryKeys[0])}&source={Uri.EscapeDataString(CurrentSource.DisplayName)}";

    public async Task<IActionResult> OnGetAsync(string sourceId, CancellationToken cancellationToken)
    {
        return await LoadAsync(sourceId, cancellationToken) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostUpdateAsync(string sourceId, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid && !await LoadAsync(sourceId, cancellationToken))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await adminApiClient.UpdateSourceAsync(sourceId, new UpdateSourceRequest
            {
                DisplayName = Source.DisplayName,
                BaseUrl = Source.BaseUrl,
                Description = Source.Description
            }, cancellationToken);

            StatusMessage = $"Updated source '{sourceId}'.";
            return RedirectToPage(new { sourceId });
        }
        catch (AdminApiValidationException exception)
        {
            AddErrors(exception);
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to update source {SourceId}.", sourceId);
            ErrorMessage = exception.Message;
        }

        await LoadAsync(sourceId, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostToggleAsync(string sourceId, bool currentlyEnabled, CancellationToken cancellationToken)
    {
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

            return RedirectToPage(new { sourceId });
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to toggle source {SourceId}.", sourceId);
            ErrorMessage = exception.Message;
            return await LoadAsync(sourceId, cancellationToken) ? Page() : NotFound();
        }
    }

    public async Task<IActionResult> OnPostCategoriesAsync(string sourceId, CancellationToken cancellationToken)
    {
        try
        {
            await adminApiClient.AssignCategoriesAsync(sourceId, new AssignSourceCategoriesRequest
            {
                CategoryKeys = CategoriesForm.CategoryKeys
            }, cancellationToken);

            StatusMessage = $"Updated categories for '{sourceId}'.";
            return RedirectToPage(new { sourceId });
        }
        catch (AdminApiValidationException exception)
        {
            AddErrors(exception);
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to update categories for source {SourceId}.", sourceId);
            ErrorMessage = exception.Message;
        }

        return await LoadAsync(sourceId, cancellationToken) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostThrottlingAsync(string sourceId, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid && !await LoadAsync(sourceId, cancellationToken))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await adminApiClient.UpdateThrottlingAsync(sourceId, new UpdateSourceThrottlingRequest
            {
                MinDelayMs = Throttling.MinDelayMs,
                MaxDelayMs = Throttling.MaxDelayMs,
                MaxConcurrentRequests = Throttling.MaxConcurrentRequests,
                RequestsPerMinute = Throttling.RequestsPerMinute,
                RespectRobotsTxt = Throttling.RespectRobotsTxt
            }, cancellationToken);

            StatusMessage = $"Updated throttling for '{sourceId}'.";
            return RedirectToPage(new { sourceId });
        }
        catch (AdminApiValidationException exception)
        {
            AddErrors(exception);
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to update throttling for source {SourceId}.", sourceId);
            ErrorMessage = exception.Message;
        }

        return await LoadAsync(sourceId, cancellationToken) ? Page() : NotFound();
    }

    private async Task<bool> LoadAsync(string sourceId, CancellationToken cancellationToken)
    {
        try
        {
            AvailableCategories = (await adminApiClient.GetCategoriesAsync(cancellationToken))
                .OrderBy(category => category.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            CurrentSource = await adminApiClient.GetSourceAsync(sourceId, cancellationToken);
            if (CurrentSource is null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(Source.DisplayName))
            {
                Source = new EditSourceInput
                {
                    DisplayName = CurrentSource.DisplayName,
                    BaseUrl = CurrentSource.BaseUrl,
                    Description = CurrentSource.Description
                };
            }

            if (CategoriesForm.CategoryKeys.Count == 0)
            {
                CategoriesForm = new CategoryAssignmentInput
                {
                    CategoryKeys = CurrentSource.SupportedCategoryKeys.ToList()
                };
            }

            if (Throttling.MaxConcurrentRequests == 0 && Throttling.RequestsPerMinute == 0)
            {
                Throttling = new ThrottlingInput
                {
                    MinDelayMs = CurrentSource.ThrottlingPolicy.MinDelayMs,
                    MaxDelayMs = CurrentSource.ThrottlingPolicy.MaxDelayMs,
                    MaxConcurrentRequests = CurrentSource.ThrottlingPolicy.MaxConcurrentRequests,
                    RequestsPerMinute = CurrentSource.ThrottlingPolicy.RequestsPerMinute,
                    RespectRobotsTxt = CurrentSource.ThrottlingPolicy.RespectRobotsTxt
                };
            }

            return true;
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to load source details for {SourceId}.", sourceId);
            ErrorMessage = exception.Message;
            return false;
        }
    }

    private void AddErrors(AdminApiValidationException exception)
    {
        foreach (var entry in exception.Errors)
        {
            foreach (var message in entry.Value)
            {
                ModelState.AddModelError(string.Empty, message);
            }
        }
    }

    public sealed class EditSourceInput
    {
        [Required]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [Url]
        public string BaseUrl { get; set; } = string.Empty;

        public string? Description { get; set; }
    }

    public sealed class CategoryAssignmentInput
    {
        public List<string> CategoryKeys { get; set; } = [];
    }

    public sealed class ThrottlingInput
    {
        [Range(0, int.MaxValue)]
        public int MinDelayMs { get; set; }

        [Range(0, int.MaxValue)]
        public int MaxDelayMs { get; set; }

        [Range(1, int.MaxValue)]
        public int MaxConcurrentRequests { get; set; } = 1;

        [Range(1, int.MaxValue)]
        public int RequestsPerMinute { get; set; } = 30;

        public bool RespectRobotsTxt { get; set; } = true;
    }
}