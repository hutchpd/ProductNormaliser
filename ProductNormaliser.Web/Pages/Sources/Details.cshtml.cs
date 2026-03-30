using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Infrastructure;
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

    [BindProperty]
    public DiscoveryProfileInput Discovery { get; set; } = new();

    [BindProperty]
    public AnalystNoteInput NoteInput { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public SourceDto? CurrentSource { get; private set; }

    public AnalystNoteDto? AnalystNote { get; private set; }

    public IReadOnlyList<CategoryMetadataDto> AvailableCategories { get; private set; } = [];

    public IReadOnlyDictionary<string, CategoryMetadataDto> CategoryLookup
        => AvailableCategories.ToDictionary(category => category.CategoryKey, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> AssignedCategoryLabels => CurrentSource is null
        ? []
        : CurrentSource.SupportedCategoryKeys
            .Select(categoryKey => CategoryLookup.TryGetValue(categoryKey, out var category) ? category.DisplayName : categoryKey)
            .ToArray();

    public bool HasDiscoveryProfileConfigured => CurrentSource is not null
        && (CurrentSource.DiscoveryProfile.CategoryEntryPages.Count > 0
            || CurrentSource.DiscoveryProfile.SitemapHints.Count > 0
            || CurrentSource.DiscoveryProfile.ProductUrlPatterns.Count > 0
            || CurrentSource.DiscoveryProfile.ListingUrlPatterns.Count > 0);

    public int ConfiguredEntryPageCount => CurrentSource?.DiscoveryProfile.CategoryEntryPages.Sum(entry => entry.Value.Count) ?? 0;

    public IReadOnlyList<KeyValuePair<string, decimal>> DiscoveryCoverageRows => CurrentSource?.DiscoveryCoverageByCategory
        .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
        .ToArray()
        ?? [];

    public string IntelligenceUrl => CurrentSource is null || CurrentSource.SupportedCategoryKeys.Count == 0
        ? "/Sources/Intelligence"
        : $"/Sources/Intelligence?category={Uri.EscapeDataString(CurrentSource.SupportedCategoryKeys[0])}&source={Uri.EscapeDataString(CurrentSource.DisplayName)}";

    public async Task<IActionResult> OnGetAsync(string sourceId, CancellationToken cancellationToken)
    {
        return await LoadAsync(sourceId, cancellationToken) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostUpdateAsync(string sourceId, CancellationToken cancellationToken)
    {
        if (!ScopedFormValidation.TryValidateActiveForm(this, Source, nameof(Source))
            && !await LoadAsync(sourceId, cancellationToken))
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
                Description = Source.Description,
                AllowedMarkets = Source.AllowedMarkets,
                PreferredLocale = Source.PreferredLocale,
                AutomationPolicy = new SourceAutomationPolicyDto
                {
                    Mode = NormalizeAutomationMode(Source.AutomationMode)
                }
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
        if (!ScopedFormValidation.TryValidateActiveForm(this, Throttling, nameof(Throttling))
            && !await LoadAsync(sourceId, cancellationToken))
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

    public async Task<IActionResult> OnPostDiscoveryAsync(string sourceId, CancellationToken cancellationToken)
    {
        if (!ScopedFormValidation.TryValidateActiveForm(this, Discovery, nameof(Discovery))
            && !await LoadAsync(sourceId, cancellationToken))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        CurrentSource = await adminApiClient.GetSourceAsync(sourceId, cancellationToken);
        if (CurrentSource is null)
        {
            return NotFound();
        }

        if (!TryBuildDiscoveryProfile(out var discoveryProfile))
        {
            await LoadAsync(sourceId, cancellationToken);
            return Page();
        }

        try
        {
            await adminApiClient.UpdateSourceAsync(sourceId, new UpdateSourceRequest
            {
                DisplayName = CurrentSource.DisplayName,
                BaseUrl = CurrentSource.BaseUrl,
                Description = CurrentSource.Description,
                DiscoveryProfile = discoveryProfile
            }, cancellationToken);

            StatusMessage = $"Updated discovery profile for '{sourceId}'.";
            return RedirectToPage(new { sourceId });
        }
        catch (AdminApiValidationException exception)
        {
            AddErrors(exception);
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to update discovery profile for source {SourceId}.", sourceId);
            ErrorMessage = exception.Message;
        }

        await LoadAsync(sourceId, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveNoteAsync(string sourceId, CancellationToken cancellationToken)
    {
        try
        {
            await adminApiClient.SaveAnalystNoteAsync(new UpsertAnalystNoteRequest
            {
                TargetType = "source",
                TargetId = sourceId,
                Title = NoteInput.Title,
                Content = NoteInput.Content
            }, cancellationToken);
            StatusMessage = $"Saved note for '{sourceId}'.";
            return RedirectToPage(new { sourceId });
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to save note for source {SourceId}.", sourceId);
            ErrorMessage = exception.Message;
            return await LoadAsync(sourceId, cancellationToken) ? Page() : NotFound();
        }
    }

    public async Task<IActionResult> OnPostDeleteNoteAsync(string sourceId, CancellationToken cancellationToken)
    {
        try
        {
            await adminApiClient.DeleteAnalystNoteAsync("source", sourceId, cancellationToken);
            StatusMessage = $"Deleted note for '{sourceId}'.";
            return RedirectToPage(new { sourceId });
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to delete note for source {SourceId}.", sourceId);
            ErrorMessage = exception.Message;
            return await LoadAsync(sourceId, cancellationToken) ? Page() : NotFound();
        }
    }

    private async Task<bool> LoadAsync(string sourceId, CancellationToken cancellationToken)
    {
        try
        {
            var categoriesTask = adminApiClient.GetCategoriesAsync(cancellationToken);
            var sourceTask = adminApiClient.GetSourceAsync(sourceId, cancellationToken);
            var noteTask = adminApiClient.GetAnalystNoteAsync("source", sourceId, cancellationToken);
            await Task.WhenAll(categoriesTask, sourceTask, noteTask);

            AvailableCategories = categoriesTask.Result
                .OrderBy(category => category.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            CurrentSource = sourceTask.Result;
            AnalystNote = noteTask.Result;
            if (CurrentSource is null)
            {
                return false;
            }

            if (AnalystNote is not null && string.IsNullOrWhiteSpace(NoteInput.Content))
            {
                NoteInput = new AnalystNoteInput
                {
                    Title = AnalystNote.Title,
                    Content = AnalystNote.Content
                };
            }

            if (string.IsNullOrWhiteSpace(Source.DisplayName))
            {
                Source = new EditSourceInput
                {
                    DisplayName = CurrentSource.DisplayName,
                    BaseUrl = CurrentSource.BaseUrl,
                    Description = CurrentSource.Description,
                    AllowedMarkets = CurrentSource.AllowedMarkets.ToList(),
                    PreferredLocale = CurrentSource.PreferredLocale,
                    AutomationMode = NormalizeAutomationMode(CurrentSource.AutomationPolicy.Mode)
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

            if (string.IsNullOrWhiteSpace(Discovery.CategoryEntryPages)
                && string.IsNullOrWhiteSpace(Discovery.SitemapHints)
                && string.IsNullOrWhiteSpace(Discovery.AllowedHosts)
                && string.IsNullOrWhiteSpace(Discovery.AllowedPathPrefixes)
                && string.IsNullOrWhiteSpace(Discovery.ExcludedPathPrefixes)
                && string.IsNullOrWhiteSpace(Discovery.ProductUrlPatterns)
                && string.IsNullOrWhiteSpace(Discovery.ListingUrlPatterns))
            {
                Discovery = new DiscoveryProfileInput
                {
                    CategoryEntryPages = FormatCategoryEntryPages(CurrentSource.DiscoveryProfile.CategoryEntryPages),
                    SitemapHints = FormatLineList(CurrentSource.DiscoveryProfile.SitemapHints),
                    AllowedHosts = FormatLineList(CurrentSource.DiscoveryProfile.AllowedHosts),
                    AllowedPathPrefixes = FormatLineList(CurrentSource.DiscoveryProfile.AllowedPathPrefixes),
                    ExcludedPathPrefixes = FormatLineList(CurrentSource.DiscoveryProfile.ExcludedPathPrefixes),
                    ProductUrlPatterns = FormatLineList(CurrentSource.DiscoveryProfile.ProductUrlPatterns),
                    ListingUrlPatterns = FormatLineList(CurrentSource.DiscoveryProfile.ListingUrlPatterns),
                    MaxDiscoveryDepth = CurrentSource.DiscoveryProfile.MaxDiscoveryDepth,
                    MaxUrlsPerRun = CurrentSource.DiscoveryProfile.MaxUrlsPerRun,
                    MaxRetryCount = CurrentSource.DiscoveryProfile.MaxRetryCount,
                    RetryBackoffBaseMs = CurrentSource.DiscoveryProfile.RetryBackoffBaseMs,
                    RetryBackoffMaxMs = CurrentSource.DiscoveryProfile.RetryBackoffMaxMs
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

    private bool TryBuildDiscoveryProfile(out SourceDiscoveryProfileDto discoveryProfile)
    {
        discoveryProfile = new SourceDiscoveryProfileDto();
        if (!TryParseCategoryEntryPages(out var categoryEntryPages))
        {
            return false;
        }

        discoveryProfile = new SourceDiscoveryProfileDto
        {
            CategoryEntryPages = categoryEntryPages,
            SitemapHints = ParseLineList(Discovery.SitemapHints),
            AllowedHosts = ParseLineList(Discovery.AllowedHosts),
            AllowedPathPrefixes = ParseLineList(Discovery.AllowedPathPrefixes),
            ExcludedPathPrefixes = ParseLineList(Discovery.ExcludedPathPrefixes),
            ProductUrlPatterns = ParseLineList(Discovery.ProductUrlPatterns),
            ListingUrlPatterns = ParseLineList(Discovery.ListingUrlPatterns),
            MaxDiscoveryDepth = Discovery.MaxDiscoveryDepth,
            MaxUrlsPerRun = Discovery.MaxUrlsPerRun,
            MaxRetryCount = Discovery.MaxRetryCount,
            RetryBackoffBaseMs = Discovery.RetryBackoffBaseMs,
            RetryBackoffMaxMs = Discovery.RetryBackoffMaxMs
        };

        return true;
    }

    private bool TryParseCategoryEntryPages(out IReadOnlyDictionary<string, IReadOnlyList<string>> categoryEntryPages)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in SplitLines(Discovery.CategoryEntryPages))
        {
            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == line.Length - 1)
            {
                ModelState.AddModelError($"{nameof(Discovery)}.{nameof(Discovery.CategoryEntryPages)}", $"Each category entry line must use 'category=url1,url2' format. Invalid value: '{line}'.");
                categoryEntryPages = result;
                return false;
            }

            var categoryKey = line[..separatorIndex].Trim();
            var urls = line[(separatorIndex + 1)..]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (urls.Length == 0)
            {
                ModelState.AddModelError($"{nameof(Discovery)}.{nameof(Discovery.CategoryEntryPages)}", $"Category '{categoryKey}' must include at least one URL.");
                categoryEntryPages = result;
                return false;
            }

            result[categoryKey] = urls;
        }

        categoryEntryPages = result;
        return true;
    }

    private static IReadOnlyList<string> ParseLineList(string? value)
    {
        return SplitLines(value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> SplitLines(string? value)
    {
        return (value ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line));
    }

    private static string FormatLineList(IReadOnlyCollection<string> values)
    {
        return values.Count == 0 ? string.Empty : string.Join(Environment.NewLine, values);
    }

    private static string FormatCategoryEntryPages(IReadOnlyDictionary<string, IReadOnlyList<string>> categoryEntryPages)
    {
        if (categoryEntryPages.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            categoryEntryPages
                .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .Select(entry => $"{entry.Key}={string.Join(", ", entry.Value)}"));
    }

    private static string NormalizeAutomationMode(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "suggest_accept" => "suggest_accept",
            "auto_accept_and_seed" => "auto_accept_and_seed",
            _ => "operator_assisted"
        };
    }

    public sealed class EditSourceInput
    {
        [Required]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [Url]
        public string BaseUrl { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Display(Name = "Allowed markets")]
        public List<string> AllowedMarkets { get; set; } = ["UK"];

        [Display(Name = "Preferred locale")]
        public string PreferredLocale { get; set; } = "en-GB";

        [Display(Name = "Automation mode")]
        public string AutomationMode { get; set; } = "operator_assisted";
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

    public sealed class DiscoveryProfileInput
    {
        [Display(Name = "Category entry pages")]
        public string? CategoryEntryPages { get; set; }

        [Display(Name = "Sitemap hints")]
        public string? SitemapHints { get; set; }

        [Display(Name = "Allowed path prefixes")]
        public string? AllowedPathPrefixes { get; set; }

        [Display(Name = "Allowed hosts")]
        public string? AllowedHosts { get; set; }

        [Display(Name = "Excluded path prefixes")]
        public string? ExcludedPathPrefixes { get; set; }

        [Display(Name = "Product URL patterns")]
        public string? ProductUrlPatterns { get; set; }

        [Display(Name = "Listing URL patterns")]
        public string? ListingUrlPatterns { get; set; }

        [Range(0, int.MaxValue)]
        [Display(Name = "Maximum discovery depth")]
        public int MaxDiscoveryDepth { get; set; } = 3;

        [Range(1, int.MaxValue)]
        [Display(Name = "Maximum URLs per run")]
        public int MaxUrlsPerRun { get; set; } = 500;

        [Range(0, int.MaxValue)]
        [Display(Name = "Maximum retries")]
        public int MaxRetryCount { get; set; } = 3;

        [Range(1, int.MaxValue)]
        [Display(Name = "Retry backoff base (ms)")]
        public int RetryBackoffBaseMs { get; set; } = 1000;

        [Range(1, int.MaxValue)]
        [Display(Name = "Retry backoff max (ms)")]
        public int RetryBackoffMaxMs { get; set; } = 30000;
    }

    public sealed class AnalystNoteInput
    {
        [StringLength(120)]
        public string? Title { get; set; }

        [StringLength(4000)]
        public string Content { get; set; } = string.Empty;
    }
}