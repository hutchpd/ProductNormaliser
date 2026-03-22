using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Models;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Pages.CrawlJobs;

public sealed class IndexModel(
    IProductNormaliserAdminApiClient adminApiClient,
    ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "selectedCategory")]
    public List<string> SeedSelectedCategoryKeys { get; set; } = [];

    [BindProperty(SupportsGet = true, Name = "category")]
    public string? CategoryKey { get; set; }

    [BindProperty]
    public LaunchCrawlJobInput Launch { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public CrawlJobListResponseDto Jobs { get; private set; } = new() { Page = 1, PageSize = 10 };

    public IReadOnlyList<CategoryMetadataDto> Categories { get; private set; } = [];

    public CategorySelectorState CategorySelector { get; private set; } = CategorySelectorStateFactory.Create([], [], $"{nameof(Launch)}.{nameof(LaunchCrawlJobInput.SelectedCategoryKeys)}", isLoading: true);

    public IReadOnlyList<SourceDto> Sources { get; private set; } = [];

    public bool SupportsRecrawlMode => false;

    public IReadOnlyList<CrawlJobDto> ActiveJobs => Jobs.Items
        .Where(job => CrawlJobPresentation.IsActiveStatus(job.Status))
        .OrderByDescending(job => job.LastUpdatedAt)
        .ToArray();

    public IReadOnlyList<CrawlJobDto> CompletedJobs => Jobs.Items
        .Where(job => CrawlJobPresentation.IsCompletedStatus(job.Status))
        .OrderByDescending(job => job.LastUpdatedAt)
        .ToArray();

    public IReadOnlyList<CrawlJobDto> FailedJobs => Jobs.Items
        .Where(job => CrawlJobPresentation.IsFailedStatus(job.Status))
        .OrderByDescending(job => job.LastUpdatedAt)
        .ToArray();

    public bool ShouldAutoRefresh => ActiveJobs.Count > 0;

    public PageHeroModel Hero => new()
    {
        Eyebrow = "Crawl Orchestration",
        Title = "Launch targeted crawls and monitor live job progress",
        Description = "The dashboard posts new crawl jobs to the Admin API, then polls the same job resource so operators can watch queued, running, and completed batches without touching the worker service directly.",
        Metrics =
        [
            new HeroMetricModel { Label = "Visible jobs", Value = Jobs.Items.Count.ToString() },
            new HeroMetricModel { Label = "Active", Value = ActiveJobs.Count.ToString() },
            new HeroMetricModel { Label = "Completed", Value = CompletedJobs.Count.ToString() },
            new HeroMetricModel { Label = "Failed", Value = FailedJobs.Count.ToString() }
        ]
    };

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostLaunchAsync(CancellationToken cancellationToken)
    {
        try
        {
            await PopulateLaunchMetadataAsync(cancellationToken);
            await LoadJobsAsync(cancellationToken);
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to load crawl job launch data before validation.");
            ErrorMessage = exception.Message;
            return Page();
        }

        if (!TryBuildRequest(out var request))
        {
            return Page();
        }

        try
        {
            var job = await adminApiClient.CreateCrawlJobAsync(request, cancellationToken);
            StatusMessage = $"Queued crawl job '{job.JobId}' for {request.RequestedCategories.Count} categories{(request.RequestedSources.Count == 0 ? string.Empty : $" across {request.RequestedSources.Count} selected sources")}.";
            return RedirectToPage("/CrawlJobs/Details", new
            {
                jobId = job.JobId,
                selectedCategory = Launch.SelectedCategoryKeys.ToArray()
            });
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
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to create crawl job from crawl jobs page.");
            ErrorMessage = exception.Message;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(string jobId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            ErrorMessage = "A job id is required to cancel a crawl.";
            await LoadAsync(cancellationToken);
            return Page();
        }

        try
        {
            var job = await adminApiClient.CancelCrawlJobAsync(jobId, cancellationToken);
            StatusMessage = $"Requested cancellation for crawl job '{job.JobId}'.";
            return RedirectToPage(new
            {
                jobId = job.JobId,
                selectedCategory = Launch.SelectedCategoryKeys.ToArray()
            });
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to cancel crawl job {JobId}.", jobId);
            ErrorMessage = exception.Message;
        }

        await LoadAsync(cancellationToken);
        return Page();
    }

    public StatusBadgeModel GetStatusBadge(string status)
    {
        var normalized = status.Replace('-', '_').ToLowerInvariant();
        return normalized switch
        {
            "running" => new StatusBadgeModel { Text = "Running", Tone = "running" },
            "pending" => new StatusBadgeModel { Text = "Pending", Tone = "pending" },
            "cancel_requested" => new StatusBadgeModel { Text = "Cancel requested", Tone = "warning" },
            "cancelled" => new StatusBadgeModel { Text = "Cancelled", Tone = "neutral" },
            "completed" => new StatusBadgeModel { Text = "Completed", Tone = "completed" },
            "completed_with_failures" => new StatusBadgeModel { Text = "Completed with failures", Tone = "warning" },
            "failed" => new StatusBadgeModel { Text = "Failed", Tone = "danger" },
            _ => new StatusBadgeModel { Text = status, Tone = "neutral" }
        };
    }

    public static int GetProgressPercent(CrawlJobDto job)
    {
        if (job.TotalTargets <= 0)
        {
            return 0;
        }

        return Math.Min(100, (int)Math.Round((double)job.ProcessedTargets / job.TotalTargets * 100d, MidpointRounding.AwayFromZero));
    }

    public static string GetScopeSummary(CrawlJobDto job)
    {
        if (job.RequestedCategories.Count > 0)
        {
            return string.Join(", ", job.RequestedCategories);
        }

        if (job.RequestedSources.Count > 0)
        {
            return string.Join(", ", job.RequestedSources);
        }

        return string.Join(", ", job.RequestedProductIds);
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await PopulateLaunchMetadataAsync(cancellationToken);
            await LoadJobsAsync(cancellationToken);
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to load crawl jobs page data.");
            ErrorMessage = exception.Message;
            Categories = [];
            Sources = [];
            Jobs = new CrawlJobListResponseDto { Page = 1, PageSize = 10 };
            CategorySelector = CategorySelectorStateFactory.Create(
                Categories,
                Launch.SelectedCategoryKeys,
                inputName: $"{nameof(Launch)}.{nameof(LaunchCrawlJobInput.SelectedCategoryKeys)}",
                errorMessage: ErrorMessage);
        }
    }

    private async Task PopulateLaunchMetadataAsync(CancellationToken cancellationToken)
    {
        var categoriesTask = adminApiClient.GetCategoriesAsync(cancellationToken);
        var sourcesTask = adminApiClient.GetSourcesAsync(cancellationToken);

        await Task.WhenAll(categoriesTask, sourcesTask);

        Categories = categoriesTask.Result.OrderBy(category => category.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
        Sources = sourcesTask.Result.OrderBy(source => source.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();

        Launch.RequestType = "category";

        if (Launch.SelectedCategoryKeys.Count == 0 && SeedSelectedCategoryKeys.Count > 0)
        {
            Launch.SelectedCategoryKeys = CategorySelectorStateFactory.NormalizeSelection(Categories, SeedSelectedCategoryKeys).ToList();
        }

        if (Launch.SelectedCategoryKeys.Count == 0 && !string.IsNullOrWhiteSpace(CategoryKey))
        {
            Launch.SelectedCategoryKeys = [CategoryKey];
        }

        CategorySelector = CategorySelectorStateFactory.Create(
            Categories,
            Launch.SelectedCategoryKeys,
            inputName: $"{nameof(Launch)}.{nameof(LaunchCrawlJobInput.SelectedCategoryKeys)}",
            emptyMessage: "No categories are available for category-targeted crawls.");
    }

    private async Task LoadJobsAsync(CancellationToken cancellationToken)
    {
        Jobs = await adminApiClient.GetCrawlJobsAsync(new CrawlJobQueryDto
        {
            RequestType = "category",
            Page = 1,
            PageSize = 30
        }, cancellationToken);
    }

    private bool TryBuildRequest(out CreateCrawlJobRequest request)
    {
        request = new CreateCrawlJobRequest();
        var categories = NormalizeValues(Launch.SelectedCategoryKeys);
        var sources = NormalizeValues(Launch.SelectedSourceIds);

        if (categories.Count == 0)
        {
            ModelState.AddModelError($"{nameof(Launch)}.{nameof(Launch.SelectedCategoryKeys)}", "Choose at least one category before launching a crawl.");
            return false;
        }

        var sourcesById = Sources.ToDictionary(source => source.SourceId, StringComparer.OrdinalIgnoreCase);
        var unknownSourceIds = sources
            .Where(sourceId => !sourcesById.ContainsKey(sourceId))
            .ToArray();

        if (unknownSourceIds.Length > 0)
        {
            ModelState.AddModelError($"{nameof(Launch)}.{nameof(Launch.SelectedSourceIds)}", $"Unknown source selections were posted: {string.Join(", ", unknownSourceIds)}.");
            return false;
        }

        var incompatibleSources = sources
            .Select(sourceId => sourcesById[sourceId])
            .Where(source => categories.All(category => !source.SupportedCategoryKeys.Contains(category, StringComparer.OrdinalIgnoreCase)))
            .Select(source => source.DisplayName)
            .ToArray();

        if (incompatibleSources.Length > 0)
        {
            ModelState.AddModelError($"{nameof(Launch)}.{nameof(Launch.SelectedSourceIds)}", $"Selected sources do not support the chosen categories: {string.Join(", ", incompatibleSources)}.");
            return false;
        }

        request = new CreateCrawlJobRequest
        {
            RequestType = "category",
            RequestedCategories = categories,
            RequestedSources = sources
        };

        return true;
    }

    private static List<string> NormalizeValues(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public sealed class LaunchCrawlJobInput
    {
        public string RequestType { get; set; } = "category";

        public List<string> SelectedCategoryKeys { get; set; } = [];

        public List<string> SelectedSourceIds { get; set; } = [];
    }
}