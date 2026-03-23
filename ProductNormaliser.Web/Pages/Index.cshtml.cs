using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Models;
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

    [BindProperty]
    public QuickCrawlInput QuickCrawl { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public StatsDto Stats { get; private set; } = new();

    public IReadOnlyList<CategoryMetadataDto> Categories { get; private set; } = [];

    public CategoryDetailDto? SelectedCategory { get; private set; }

    public IReadOnlyList<SourceDto> Sources { get; private set; } = [];

    public IReadOnlyList<CrawlJobDto> RecentJobs { get; private set; } = [];

    public PageHeroModel Hero => new()
    {
        Eyebrow = "Operations Dashboard",
        Title = "Internal control surface for category, source, product, and crawl orchestration",
        Description = "Razor Pages is the pragmatic fit here: rich enough for an internal dashboard, server-rendered by default, and simple to wire into the Admin API with typed HTTP clients and page-model state.",
        Metrics =
        [
            new HeroMetricModel { Label = "Enabled categories", Value = Categories.Count(category => category.IsEnabled).ToString() },
            new HeroMetricModel { Label = "Managed sources", Value = Sources.Count.ToString() },
            new HeroMetricModel { Label = "Canonical products", Value = Stats.TotalCanonicalProducts.ToString() },
            new HeroMetricModel { Label = "Recent jobs", Value = RecentJobs.Count.ToString() }
        ]
    };

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadDashboardAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostLaunchCategoryCrawlAsync(CancellationToken cancellationToken)
    {
        var categoryKey = string.IsNullOrWhiteSpace(QuickCrawl.CategoryKey)
            ? SelectedCategoryKey
            : QuickCrawl.CategoryKey;

        if (string.IsNullOrWhiteSpace(categoryKey))
        {
            ModelState.AddModelError($"{nameof(QuickCrawl)}.{nameof(QuickCrawl.CategoryKey)}", "Select a category before launching a crawl job.");
            await LoadDashboardAsync(cancellationToken);
            return Page();
        }

        try
        {
            var job = await adminApiClient.CreateCrawlJobAsync(new CreateCrawlJobRequest
            {
                RequestType = "category",
                RequestedCategories = [categoryKey]
            }, cancellationToken);

            StatusMessage = $"Queued crawl job '{job.JobId}' for category '{categoryKey}'.";
            return RedirectToPage("/CrawlJobs/Index", new { jobId = job.JobId, category = categoryKey });
        }
        catch (AdminApiValidationException exception)
        {
            AddValidationErrors(exception);
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to create crawl job from the dashboard.");
            ErrorMessage = exception.Message;
        }

        await LoadDashboardAsync(cancellationToken);
        return Page();
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

    private async Task LoadDashboardAsync(CancellationToken cancellationToken)
    {
        try
        {
            var categoriesTask = adminApiClient.GetCategoriesAsync(cancellationToken);
            var sourcesTask = adminApiClient.GetSourcesAsync(cancellationToken);
            var statsTask = adminApiClient.GetStatsAsync(cancellationToken);
            var jobsTask = adminApiClient.GetCrawlJobsAsync(new CrawlJobQueryDto { Page = 1, PageSize = 5 }, cancellationToken);

            await Task.WhenAll(categoriesTask, sourcesTask, statsTask, jobsTask);

            Categories = InteractiveCategoryFilter.Apply(categoriesTask.Result);
            Sources = sourcesTask.Result.OrderBy(source => source.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
            Stats = statsTask.Result;
            RecentJobs = jobsTask.Result.Items;

            var effectiveCategoryKey = SelectedCategoryKey;
            if (string.IsNullOrWhiteSpace(effectiveCategoryKey))
            {
                effectiveCategoryKey = Categories.FirstOrDefault(category => category.IsEnabled)?.CategoryKey
                    ?? Categories.FirstOrDefault()?.CategoryKey;
            }

            if (!string.IsNullOrWhiteSpace(effectiveCategoryKey))
            {
                SelectedCategory = await adminApiClient.GetCategoryDetailAsync(effectiveCategoryKey, cancellationToken);
                SelectedCategoryKey = SelectedCategory?.Metadata.CategoryKey ?? effectiveCategoryKey;
            }

            if (string.IsNullOrWhiteSpace(QuickCrawl.CategoryKey) && !string.IsNullOrWhiteSpace(SelectedCategoryKey))
            {
                QuickCrawl.CategoryKey = SelectedCategoryKey;
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
            RecentJobs = [];
            Stats = new StatsDto();
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

    public sealed class QuickCrawlInput
    {
        [Required]
        public string CategoryKey { get; set; } = string.Empty;
    }
}
