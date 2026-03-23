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
    public enum OperatorLandingState
    {
        Loading,
        Ready,
        Empty,
        Error
    }

    [BindProperty(SupportsGet = true, Name = "category")]
    public string? SelectedCategoryKey { get; set; }

    [BindProperty]
    public QuickCrawlInput QuickCrawl { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public StatsDto Stats { get; private set; } = new();

    public IReadOnlyList<CategoryMetadataDto> Categories { get; private set; } = [];

    public CategoryContextState? CurrentCategoryContext { get; private set; }

    public CategoryDetailDto? SelectedCategory { get; private set; }

    public IReadOnlyList<SourceDto> Sources { get; private set; } = [];

    public IReadOnlyList<CrawlJobDto> RecentJobs { get; private set; } = [];

    public OperatorLandingState LandingState { get; private set; } = OperatorLandingState.Loading;

    public bool HasCategoryContext => CurrentCategoryContext?.HasSelection == true;

    public IReadOnlyList<CrawlJobDto> ActiveJobs => RecentJobs
        .Where(job => CrawlJobPresentation.IsActiveStatus(job.Status))
        .OrderByDescending(job => job.LastUpdatedAt)
        .ToArray();

    public IReadOnlyList<SourceDto> CategorySources => Sources
        .Where(source => CurrentCategoryContext?.SelectedCategoryKeys.Count > 0
            && source.SupportedCategoryKeys.Any(categoryKey => CurrentCategoryContext.SelectedCategoryKeys.Contains(categoryKey, StringComparer.OrdinalIgnoreCase)))
        .OrderByDescending(source => source.IsEnabled)
        .ThenByDescending(source => source.ThrottlingPolicy.RequestsPerMinute)
        .ThenBy(source => source.DisplayName, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public int EnabledCategorySourceCount => CategorySources.Count(source => source.IsEnabled);

    public int ReadyCategorySourceCount => CategorySources.Count(source => string.Equals(source.Readiness.Status, "Ready", StringComparison.OrdinalIgnoreCase));

    public int AttentionCategorySourceCount => CategorySources.Count(source => string.Equals(source.Health.Status, "Watch", StringComparison.OrdinalIgnoreCase)
        || string.Equals(source.Health.Status, "Attention", StringComparison.OrdinalIgnoreCase));

    public int RobotsProtectedSourceCount => CategorySources.Count(source => source.ThrottlingPolicy.RespectRobotsTxt);

    public decimal AverageRequestsPerMinute => CategorySources.Count == 0
        ? 0m
        : decimal.Round(CategorySources.Average(source => (decimal)source.ThrottlingPolicy.RequestsPerMinute), 0, MidpointRounding.AwayFromZero);

    public decimal SchemaCompletenessPercent => SelectedCategory?.Metadata.SchemaCompletenessScore is decimal score
        ? decimal.Round(score * 100m, 0, MidpointRounding.AwayFromZero)
        : 0m;

    public string ConflictRateSummary => $"{Stats.PercentProductsWithConflicts:0.#}%";

    public string MissingKeySummary => $"{Stats.PercentProductsMissingKeyAttributes:0.#}%";

    public IReadOnlyList<OperatorSummaryCardModel> ProductSummaryCards =>
    [
        new OperatorSummaryCardModel
        {
            Title = "Canonical products",
            Value = Stats.TotalCanonicalProducts.ToString(),
            Description = HasCategoryContext
                ? $"Current context: {CurrentCategoryContext!.SelectionSummary}."
                : "No category context is active yet.",
            Tone = "completed"
        },
        new OperatorSummaryCardModel
        {
            Title = "Source products",
            Value = Stats.TotalSourceProducts.ToString(),
            Description = "Total scraped products feeding canonicalization.",
            Tone = "neutral"
        },
        new OperatorSummaryCardModel
        {
            Title = "Conflict rate",
            Value = ConflictRateSummary,
            Description = "Products currently carrying cross-source disagreement.",
            Tone = Stats.PercentProductsWithConflicts >= 25m ? "warning" : "completed"
        },
        new OperatorSummaryCardModel
        {
            Title = "Missing key fields",
            Value = MissingKeySummary,
            Description = "Products missing category-defining key attributes.",
            Tone = Stats.PercentProductsMissingKeyAttributes >= 20m ? "warning" : "neutral"
        }
    ];

    public IReadOnlyList<OperatorSummaryCardModel> QualitySummaryCards =>
    [
        new OperatorSummaryCardModel
        {
            Title = "Schema readiness",
            Value = HasCategoryContext ? $"{SchemaCompletenessPercent:0}%" : "No category",
            Description = HasCategoryContext
                ? $"{SelectedCategory?.Schema.Attributes.Count ?? 0} tracked attributes in the active primary category."
                : "Select an enabled category to inspect schema readiness.",
            Tone = SchemaCompletenessPercent >= 85m ? "completed" : "warning"
        },
        new OperatorSummaryCardModel
        {
            Title = "Avg attributes",
            Value = Stats.AverageAttributesPerProduct.ToString("0.0"),
            Description = "Average normalized attributes per canonical product.",
            Tone = "neutral"
        }
    ];

    public IReadOnlyList<OperatorSummaryCardModel> SourceHealthCards =>
    [
        new OperatorSummaryCardModel
        {
            Title = "Sources in context",
            Value = CategorySources.Count.ToString(),
            Description = HasCategoryContext
                ? "Sources matching the selected category set."
                : "Choose categories to scope source operations.",
            Tone = "neutral"
        },
        new OperatorSummaryCardModel
        {
            Title = "Crawl-ready",
            Value = ReadyCategorySourceCount.ToString(),
            Description = "Sources with assigned category coverage ready for crawl launch.",
            Tone = ReadyCategorySourceCount == 0 ? "warning" : "completed"
        },
        new OperatorSummaryCardModel
        {
            Title = "Needs attention",
            Value = AttentionCategorySourceCount.ToString(),
            Description = "Sources currently reporting watch or attention health posture.",
            Tone = AttentionCategorySourceCount == 0 ? "completed" : "warning"
        },
        new OperatorSummaryCardModel
        {
            Title = "Avg requests/min",
            Value = AverageRequestsPerMinute.ToString("0"),
            Description = "Average configured throughput across sources in scope.",
            Tone = "neutral"
        }
    ];

    public IReadOnlyList<OperatorActionCardModel> ActionCards =>
    [
        new OperatorActionCardModel
        {
            Eyebrow = "Start Crawl",
            Title = "Launch targeted crawl work",
            Description = "Jump straight into category-scoped crawl orchestration with the current context already staged.",
            Href = CrawlJobsUrl,
            AccentValue = HasCategoryContext ? CurrentCategoryContext!.SelectionSummary : "No category context",
            AccentLabel = "Current crawl scope"
        },
        new OperatorActionCardModel
        {
            Eyebrow = "View Jobs",
            Title = "Monitor live and historical jobs",
            Description = "Open the job console to watch queue progress, failures, and completed work by category.",
            Href = CrawlJobsUrl,
            AccentValue = ActiveJobs.Count.ToString(),
            AccentLabel = "Active jobs"
        },
        new OperatorActionCardModel
        {
            Eyebrow = "Explore Products",
            Title = "Inspect canonical product output",
            Description = "Move into the product explorer with the current primary category already selected.",
            Href = ProductsUrl,
            AccentValue = Stats.TotalCanonicalProducts.ToString(),
            AccentLabel = "Canonical products"
        },
        new OperatorActionCardModel
        {
            Eyebrow = "Review Quality",
            Title = "Audit quality and disagreement",
            Description = "Review coverage, conflict, backlog, and stability for the current category lens.",
            Href = QualityUrl,
            AccentValue = ConflictRateSummary,
            AccentLabel = "Conflict rate"
        },
        new OperatorActionCardModel
        {
            Eyebrow = "Manage Sources",
            Title = "Review source health and coverage",
            Description = "Open source management to adjust enabled sources, throttling posture, and category coverage.",
            Href = SourcesUrl,
            AccentValue = EnabledCategorySourceCount.ToString(),
            AccentLabel = "Enabled in scope"
        }
    ];

    public string CategorySelectionUrl => BuildUrl("/Categories/Index", includePrimaryCategory: false);

    public string CrawlJobsUrl => BuildUrl("/CrawlJobs/Index", includePrimaryCategory: false);

    public string ProductsUrl => BuildUrl("/Products/Index");

    public string QualityUrl => BuildUrl("/Quality/Index");

    public string SourcesUrl => BuildUrl("/Sources/Index");

    public string SourceIntelligenceUrl => BuildUrl("/Sources/Intelligence");

    public PageHeroModel Hero => new()
    {
        Eyebrow = "Operator Console",
        Title = HasCategoryContext
            ? $"Milestone 1 operations for {CurrentCategoryContext!.SelectionSummary}"
            : "Milestone 1 operator landing",
        Description = HasCategoryContext
            ? "Use the current category context to launch crawl work, monitor active jobs, inspect product health, and review source posture without leaving the control surface."
            : "This console is the deliberate entry point for crawl, quality, products, and sources. Select active categories first, then move into the main operating paths.",
        Metrics =
        [
            new HeroMetricModel { Label = "Context", Value = HasCategoryContext ? CurrentCategoryContext!.SelectionSummary : "No category" },
            new HeroMetricModel { Label = "Active jobs", Value = ActiveJobs.Count.ToString() },
            new HeroMetricModel { Label = "Canonical products", Value = Stats.TotalCanonicalProducts.ToString() },
            new HeroMetricModel { Label = "Sources in scope", Value = CategorySources.Count.ToString() }
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
            return RedirectToPage("/CrawlJobs/Index", new { jobId = job.JobId, selectedCategory = CurrentCategoryContext?.SelectedCategoryKeys.ToArray() ?? [categoryKey] });
        }
        catch (AdminApiValidationException exception)
        {
            foreach (var entry in exception.Errors)
            {
                foreach (var message in entry.Value)
                {
                    ModelState.AddModelError($"{nameof(QuickCrawl)}.{nameof(QuickCrawl.CategoryKey)}", message);
                }
            }
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to create crawl job from the dashboard.");
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
            CurrentCategoryContext = CategoryContextStateFactory.Resolve(
                Categories,
                SelectedCategoryKey,
                null,
                PageContext?.HttpContext?.Request.Cookies[CategoryContextState.CookieName]);
            Sources = sourcesTask.Result.OrderBy(source => source.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
            Stats = statsTask.Result;
            RecentJobs = jobsTask.Result.Items;

            var effectiveCategoryKey = CurrentCategoryContext.PrimaryCategoryKey;

            if (!string.IsNullOrWhiteSpace(effectiveCategoryKey))
            {
                SelectedCategory = await adminApiClient.GetCategoryDetailAsync(effectiveCategoryKey, cancellationToken);
                SelectedCategoryKey = SelectedCategory?.Metadata.CategoryKey ?? effectiveCategoryKey;
            }

            if (string.IsNullOrWhiteSpace(QuickCrawl.CategoryKey) && !string.IsNullOrWhiteSpace(SelectedCategoryKey))
            {
                QuickCrawl.CategoryKey = SelectedCategoryKey;
            }

            LandingState = Categories.Count == 0 ? OperatorLandingState.Empty : OperatorLandingState.Ready;
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to load dashboard data from Admin API.");
            ErrorMessage = exception.Message;
            Categories = [];
            CurrentCategoryContext = null;
            Sources = [];
            SelectedCategory = null;
            RecentJobs = [];
            Stats = new StatsDto();
            LandingState = OperatorLandingState.Error;
        }
    }

    public sealed class QuickCrawlInput
    {
        [Required]
        public string CategoryKey { get; set; } = string.Empty;
    }

    private string BuildUrl(string basePath, bool includePrimaryCategory = true)
    {
        var queryParts = new List<string>();

        if (includePrimaryCategory && !string.IsNullOrWhiteSpace(SelectedCategoryKey))
        {
            queryParts.Add($"category={Uri.EscapeDataString(SelectedCategoryKey)}");
        }

        foreach (var categoryKey in CurrentCategoryContext?.SelectedCategoryKeys ?? [])
        {
            queryParts.Add($"selectedCategory={Uri.EscapeDataString(categoryKey)}");
        }

        if (queryParts.Count == 0)
        {
            return basePath;
        }

        return $"{basePath}?{string.Join("&", queryParts)}";
    }
}
