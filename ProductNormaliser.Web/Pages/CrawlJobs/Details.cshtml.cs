using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Models;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Pages.CrawlJobs;

public sealed class DetailsModel(
    IProductNormaliserAdminApiClient adminApiClient,
    ILogger<DetailsModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "jobId")]
    public string JobId { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true, Name = "selectedCategory")]
    public List<string> SelectedCategoryKeys { get; set; } = [];

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public CrawlJobDto? Job { get; private set; }

    public bool ShouldAutoRefresh => Job is not null && CrawlJobPresentation.IsActiveStatus(Job.Status);

    public StatusBadgeModel StatusBadge => CrawlJobPresentation.GetStatusBadge(Job?.Status);

    public int ProgressPercent => Job is null ? 0 : CrawlJobPresentation.GetProgressPercent(Job);

    public decimal DiscoveryProgressPercent => Job?.DiscoveryCompletionPercent ?? 0m;

    public decimal ProductProgressPercent => Job is null || Job.ConfirmedProductTargetCount == 0
        ? 0m
        : decimal.Round((decimal)Job.CrawledProductUrlCount / Job.ConfirmedProductTargetCount * 100m, 2, MidpointRounding.AwayFromZero);

    public IReadOnlyList<string> EffectiveSelectedCategoryKeys { get; private set; } = [];

    public string BackToJobsUrl => BuildBackToJobsUrl(EffectiveSelectedCategoryKeys);

    public PageHeroModel Hero => Job is null
        ? new PageHeroModel
        {
            Eyebrow = "Crawl Job",
            Title = "Crawl job details",
            Description = "Review a single crawl job, its current progress, and its per-category outcome counts.",
            Metrics = []
        }
        : new PageHeroModel
        {
            Eyebrow = "Crawl Job",
            Title = Job.JobId,
            Description = "This page tracks aggregate crawl-job progress rather than raw queue records, including current status, outcome counts, and per-category breakdowns.",
            Metrics =
            [
                new HeroMetricModel { Label = "Discovery", Value = $"{DiscoveryProgressPercent:0.#}%" },
                new HeroMetricModel { Label = "Products", Value = $"{ProductProgressPercent:0.#}%" },
                new HeroMetricModel { Label = "Processed", Value = $"{Job.ProcessedTargets}/{Job.TotalTargets}" },
                new HeroMetricModel { Label = "Status", Value = StatusBadge.Text }
            ]
        };

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostCancelAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(JobId))
        {
            ErrorMessage = "A job id is required to cancel a crawl.";
            await LoadAsync(cancellationToken);
            return Page();
        }

        try
        {
            var job = await adminApiClient.CancelCrawlJobAsync(JobId, cancellationToken);
            StatusMessage = $"Requested cancellation for crawl job '{job.JobId}'.";
            return RedirectToPage(new { jobId = job.JobId, selectedCategory = SelectedCategoryKeys.ToArray() });
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to cancel crawl job {JobId} from details page.", JobId);
            ErrorMessage = exception.Message;
        }

        await LoadAsync(cancellationToken);
        return Page();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var categories = InteractiveCategoryFilter.Apply(await adminApiClient.GetCategoriesAsync(cancellationToken));
            var categoryContext = CategoryContextStateFactory.Resolve(
                categories,
                null,
                SelectedCategoryKeys,
                PageContext?.HttpContext?.Request.Cookies[CategoryContextState.CookieName]);
            EffectiveSelectedCategoryKeys = categoryContext.SelectedCategoryKeys;

            if (string.IsNullOrWhiteSpace(JobId))
            {
                ErrorMessage = "Select a crawl job to view its progress details.";
                Job = null;
                return;
            }

            Job = await adminApiClient.GetCrawlJobAsync(JobId, cancellationToken);
            if (Job is null)
            {
                ErrorMessage = $"Crawl job '{JobId}' was not found.";
            }
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to load crawl job {JobId}.", JobId);
            ErrorMessage = exception.Message;
            Job = null;
        }
    }

    private static string BuildBackToJobsUrl(IReadOnlyList<string> selectedCategoryKeys)
    {
        if (selectedCategoryKeys.Count == 0)
        {
            return "/CrawlJobs/Index";
        }

        var query = string.Join("&", selectedCategoryKeys.Select(categoryKey => $"selectedCategory={Uri.EscapeDataString(categoryKey)}"));
        return $"/CrawlJobs/Index?{query}";
    }
}