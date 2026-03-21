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

    [BindProperty(SupportsGet = true, Name = "status")]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true, Name = "requestType")]
    public string? RequestType { get; set; }

    [BindProperty(SupportsGet = true, Name = "category")]
    public string? CategoryKey { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true, Name = "jobId")]
    public string? SelectedJobId { get; set; }

    [BindProperty]
    public LaunchCrawlJobInput Launch { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public CrawlJobListResponseDto Jobs { get; private set; } = new() { Page = 1, PageSize = 10 };

    public CrawlJobDto? SelectedJob { get; private set; }

    public IReadOnlyList<CategoryMetadataDto> Categories { get; private set; } = [];

    public CategorySelectorState CategorySelector { get; private set; } = CategorySelectorStateFactory.Create([], [], $"{nameof(Launch)}.{nameof(LaunchCrawlJobInput.SelectedCategoryKeys)}", isLoading: true);

    public IReadOnlyList<SourceDto> Sources { get; private set; } = [];

    public bool ShouldAutoRefresh => SelectedJob is not null && IsActiveJob(SelectedJob.Status);

    public PaginationModel Pagination => new()
    {
        PagePath = "/CrawlJobs/Index",
        CurrentPage = Jobs.Page,
        TotalPages = Jobs.TotalPages,
        TotalCount = Jobs.TotalCount,
        RouteValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["status"] = Status,
            ["requestType"] = RequestType,
            ["category"] = CategoryKey,
            ["jobId"] = SelectedJobId
        }
    };

    public PageHeroModel Hero => new()
    {
        Eyebrow = "Crawl Orchestration",
        Title = "Launch targeted crawls and monitor live job progress",
        Description = "The dashboard posts new crawl jobs to the Admin API, then polls the same job resource so operators can watch queued, running, and completed batches without touching the worker service directly.",
        Metrics =
        [
            new HeroMetricModel { Label = "Visible jobs", Value = Jobs.Items.Count.ToString() },
            new HeroMetricModel { Label = "Running", Value = Jobs.Items.Count(job => string.Equals(job.Status, "running", StringComparison.OrdinalIgnoreCase)).ToString() },
            new HeroMetricModel { Label = "Pending", Value = Jobs.Items.Count(job => string.Equals(job.Status, "pending", StringComparison.OrdinalIgnoreCase)).ToString() }
        ]
    };

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostLaunchAsync(CancellationToken cancellationToken)
    {
        if (!TryBuildRequest(out var request))
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        try
        {
            var job = await adminApiClient.CreateCrawlJobAsync(request, cancellationToken);
            StatusMessage = $"Queued crawl job '{job.JobId}'.";
            return RedirectToPage(new
            {
                jobId = job.JobId,
                category = CategoryKey,
                requestType = RequestType,
                status = Status,
                page = 1,
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

        await LoadAsync(cancellationToken);
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
                category = CategoryKey,
                requestType = RequestType,
                status = Status,
                page = PageNumber,
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
            var categoriesTask = adminApiClient.GetCategoriesAsync(cancellationToken);
            var sourcesTask = adminApiClient.GetSourcesAsync(cancellationToken);
            var jobsTask = adminApiClient.GetCrawlJobsAsync(new CrawlJobQueryDto
            {
                Status = Status,
                RequestType = RequestType,
                CategoryKey = CategoryKey,
                Page = Math.Max(1, PageNumber),
                PageSize = 10
            }, cancellationToken);

            await Task.WhenAll(categoriesTask, sourcesTask, jobsTask);

            Categories = categoriesTask.Result.OrderBy(category => category.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
            Sources = sourcesTask.Result.OrderBy(source => source.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
            Jobs = jobsTask.Result;

            if (string.IsNullOrWhiteSpace(Launch.RequestType))
            {
                Launch.RequestType = "category";
            }

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

            if (!string.IsNullOrWhiteSpace(SelectedJobId))
            {
                SelectedJob = await adminApiClient.GetCrawlJobAsync(SelectedJobId, cancellationToken);
            }
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

    private bool TryBuildRequest(out CreateCrawlJobRequest request)
    {
        request = new CreateCrawlJobRequest();
        var requestType = NormalizeRequestType(Launch.RequestType);
        if (requestType is null)
        {
            ModelState.AddModelError($"{nameof(Launch)}.{nameof(Launch.RequestType)}", "Select a supported crawl scope.");
            return false;
        }

        var categories = NormalizeValues(Launch.SelectedCategoryKeys);
        var sources = NormalizeValues(Launch.SelectedSourceIds);
        var productIds = ParseLines(Launch.ProductIdsText);

        switch (requestType)
        {
            case "category" when categories.Count == 0:
                ModelState.AddModelError($"{nameof(Launch)}.{nameof(Launch.SelectedCategoryKeys)}", "Choose at least one category for a category crawl.");
                return false;
            case "source" when sources.Count == 0:
                ModelState.AddModelError($"{nameof(Launch)}.{nameof(Launch.SelectedSourceIds)}", "Choose at least one source for a source crawl.");
                return false;
            case "product_selection" when productIds.Count == 0:
                ModelState.AddModelError($"{nameof(Launch)}.{nameof(Launch.ProductIdsText)}", "Enter at least one canonical product id for a product recrawl.");
                return false;
        }

        request = new CreateCrawlJobRequest
        {
            RequestType = requestType,
            RequestedCategories = categories,
            RequestedSources = sources,
            RequestedProductIds = productIds
        };

        return true;
    }

    private static string? NormalizeRequestType(string? requestType)
    {
        if (string.IsNullOrWhiteSpace(requestType))
        {
            return null;
        }

        var normalized = requestType.Trim().Replace('-', '_').ToLowerInvariant();
        return normalized is "category" or "source" or "product_selection"
            ? normalized
            : null;
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

    private static List<string> ParseLines(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text
            .Split([',', '\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsActiveJob(string status)
    {
        return string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "running", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "cancel_requested", StringComparison.OrdinalIgnoreCase);
    }

    public sealed class LaunchCrawlJobInput
    {
        [Required]
        public string RequestType { get; set; } = "category";

        public List<string> SelectedCategoryKeys { get; set; } = [];

        public List<string> SelectedSourceIds { get; set; } = [];

        public string? ProductIdsText { get; set; }
    }
}