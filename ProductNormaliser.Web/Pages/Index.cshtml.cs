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

    [BindProperty]
    public ManageCategorySchemaInput CategorySchema { get; set; } = new();

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

    public int BootReadyCategorySourceCount => CategorySources.Count(source => source.IsEnabled
        && HasDiscoveryScaffold(source)
        && source.Readiness.CrawlableCategoryCount > 0);

    public int ReadyCategorySourceCount => CategorySources.Count(source => string.Equals(source.Readiness.Status, "Ready", StringComparison.OrdinalIgnoreCase));

    public int AttentionCategorySourceCount => CategorySources.Count(source => string.Equals(source.Health.Status, "Watch", StringComparison.OrdinalIgnoreCase)
        || string.Equals(source.Health.Status, "Attention", StringComparison.OrdinalIgnoreCase));

    public int RobotsProtectedSourceCount => CategorySources.Count(source => source.ThrottlingPolicy.RespectRobotsTxt);

    public decimal AverageRequestsPerMinute => CategorySources.Count == 0
        ? 0m
        : decimal.Round(CategorySources.Average(source => (decimal)source.ThrottlingPolicy.RequestsPerMinute), 0, MidpointRounding.AwayFromZero);

    public int EstimatedContextSeedCount => CategorySources
        .Where(source => source.IsEnabled)
        .Sum(source => EstimateSeedCount(source, CurrentCategoryContext?.SelectedCategoryKeys ?? []));

    public decimal SchemaCompletenessPercent => SelectedCategory?.Metadata.SchemaCompletenessScore is decimal score
        ? decimal.Round(score * 100m, 0, MidpointRounding.AwayFromZero)
        : 0m;

    public IReadOnlyList<CategoryOperationalMetricDto> SelectedCategoryOperationalMetrics => HasCategoryContext
        ? Stats.Operational.Categories
            .Where(metric => CurrentCategoryContext!.SelectedCategoryKeys.Contains(metric.CategoryKey, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(metric => metric.DiscoveryQueueDepth)
            .ThenByDescending(metric => metric.QueueDepth)
            .ThenBy(metric => metric.CategoryKey, StringComparer.OrdinalIgnoreCase)
            .ToArray()
        : [];

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

    public IReadOnlyList<OperatorSummaryCardModel> DiscoverySummaryCards =>
    [
        new OperatorSummaryCardModel
        {
            Title = "Discovery queue",
            Value = Stats.DiscoveryQueueDepth.ToString(),
            Description = "Queued or processing discovery frontier items across all sources.",
            Tone = Stats.DiscoveryQueueDepth >= 50 ? "warning" : "neutral"
        },
        new OperatorSummaryCardModel
        {
            Title = "Processed per hour",
            Value = Stats.DiscoveryProcessingRateLast24Hours.ToString("0.##"),
            Description = $"{Stats.DiscoveredUrlCountLast24Hours} newly seen URLs, {Stats.RejectedUrlCountLast24Hours} rejected, {Stats.RobotsBlockedCountLast24Hours} blocked in 24h.",
            Tone = Stats.DiscoveryProcessingRateLast24Hours == 0 ? "warning" : "completed"
        },
        new OperatorSummaryCardModel
        {
            Title = "Confirmed products 24h",
            Value = Stats.ConfirmedProductUrlCountLast24Hours.ToString(),
            Description = "Discovered product URLs promoted into the product crawl queue in the trailing 24 hours.",
            Tone = Stats.ConfirmedProductUrlCountLast24Hours == 0 ? "warning" : "completed"
        },
        new OperatorSummaryCardModel
        {
            Title = "Active discovery sources",
            Value = Stats.ActiveDiscoverySourceCount.ToString(),
            Description = "Sources with current discovery backlog or recent discovery activity.",
            Tone = Stats.ActiveDiscoverySourceCount == 0 ? "warning" : "neutral"
        }
    ];

    public IReadOnlyList<OperatorSummaryCardModel> OperationalHealthCards =>
    [
        new OperatorSummaryCardModel
        {
            Title = "Queue depth",
            Value = Stats.Operational.QueueDepth.ToString(),
            Description = "Queued or processing crawl targets waiting on worker capacity.",
            Tone = Stats.Operational.QueueDepth >= 25 ? "warning" : "neutral"
        },
        new OperatorSummaryCardModel
        {
            Title = "Retry backlog",
            Value = Stats.Operational.RetryQueueDepth.ToString(),
            Description = "Targets already retried and still waiting for another attempt.",
            Tone = Stats.Operational.RetryQueueDepth == 0 ? "completed" : "warning"
        },
        new OperatorSummaryCardModel
        {
            Title = "Failures 24h",
            Value = Stats.Operational.FailureCountLast24Hours.ToString(),
            Description = "Failed crawl attempts recorded in the trailing 24 hours.",
            Tone = Stats.Operational.FailureCountLast24Hours == 0 ? "completed" : "warning"
        },
        new OperatorSummaryCardModel
        {
            Title = "Throughput 24h",
            Value = Stats.Operational.ThroughputLast24Hours.ToString(),
            Description = "Total crawl attempts observed in the trailing 24 hours.",
            Tone = Stats.Operational.ThroughputLast24Hours == 0 ? "warning" : "completed"
        }
    ];

    public IReadOnlyList<SourceOperationalMetricDto> AtRiskOperationalSources => Stats.Operational.Sources
        .Where(metric => !string.Equals(metric.HealthStatus, "Healthy", StringComparison.OrdinalIgnoreCase)
            || metric.RetryQueueDepth > 0
            || metric.FailedQueueDepth > 0
            || metric.FailedCrawlsLast24Hours > 0)
        .OrderByDescending(metric => metric.FailureRateLast24Hours)
        .ThenByDescending(metric => metric.RetryQueueDepth)
        .ThenBy(metric => metric.SourceName, StringComparer.OrdinalIgnoreCase)
        .Take(5)
        .ToArray();

    public IReadOnlyList<CategoryOperationalMetricDto> BusyOperationalCategories => Stats.Operational.Categories
        .Where(metric => metric.QueueDepth > 0 || metric.DiscoveryQueueDepth > 0 || metric.FailedCrawlsLast24Hours > 0 || metric.ActiveJobCount > 0)
        .OrderByDescending(metric => metric.DiscoveryQueueDepth)
        .ThenByDescending(metric => metric.QueueDepth)
        .ThenByDescending(metric => metric.FailedCrawlsLast24Hours)
        .ThenBy(metric => metric.CategoryKey, StringComparer.OrdinalIgnoreCase)
        .Take(5)
        .ToArray();

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
        },
        new OperatorActionCardModel
        {
            Eyebrow = "Operational Health",
            Title = "Investigate retries and failures",
            Description = "Use source intelligence and crawl jobs together when retry backlog, queue pressure, or category failures start climbing.",
            Href = SourceIntelligenceUrl,
            AccentValue = Stats.Operational.RetryQueueDepth.ToString(),
            AccentLabel = "Retry backlog"
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
            new HeroMetricModel { Label = "Discovery queue", Value = Stats.DiscoveryQueueDepth.ToString() },
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

    public async Task<IActionResult> OnPostSaveCategorySchemaAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(CategorySchema.CategoryKey))
        {
            ModelState.AddModelError(nameof(CategorySchema.CategoryKey), "Select a category before updating its attribute profile.");
            await LoadDashboardAsync(cancellationToken);
            return Page();
        }

        PrepareCategorySchemaDraft();
        ValidateCategorySchemaDraft();
        if (!ModelState.IsValid)
        {
            await LoadDashboardAsync(cancellationToken);
            return Page();
        }

        try
        {
            var updatedSchema = await adminApiClient.UpdateCategorySchemaAsync(
                CategorySchema.CategoryKey,
                BuildUpdateCategorySchemaRequest(CategorySchema.Attributes),
                cancellationToken);

            StatusMessage = $"Updated the quality summary attribute profile for {updatedSchema.DisplayName}.";
            return RedirectToPage("/Index", BuildCurrentRouteValues(CategorySchema.CategoryKey));
        }
        catch (AdminApiValidationException exception)
        {
            foreach (var entry in exception.Errors)
            {
                foreach (var message in entry.Value)
                {
                    ModelState.AddModelError(string.IsNullOrWhiteSpace(entry.Key) ? string.Empty : entry.Key, message);
                }
            }
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to update category schema for the dashboard.");
            ErrorMessage = exception.Message;
        }

        await LoadDashboardAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostToggleCategorySchemaRequiredAsync(string categoryKey, string attributeKey, bool isRequired, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(categoryKey))
        {
            return new JsonResult(new { message = "Select a category before updating its schema." })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        if (string.IsNullOrWhiteSpace(attributeKey))
        {
            return new JsonResult(new { message = "Select an attribute before updating its required status." })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        try
        {
            var categoryDetail = await adminApiClient.GetCategoryDetailAsync(categoryKey, cancellationToken);
            if (categoryDetail is null)
            {
                return new JsonResult(new { message = "The selected category schema could not be found." })
                {
                    StatusCode = StatusCodes.Status404NotFound
                };
            }

            var targetAttribute = categoryDetail.Schema.Attributes.FirstOrDefault(attribute => string.Equals(attribute.Key, attributeKey, StringComparison.OrdinalIgnoreCase));
            if (targetAttribute is null)
            {
                return new JsonResult(new { message = $"The attribute '{attributeKey}' is not part of the selected category schema." })
                {
                    StatusCode = StatusCodes.Status404NotFound
                };
            }

            var updatedSchema = await adminApiClient.UpdateCategorySchemaAsync(
                categoryKey,
                new UpdateCategorySchemaRequest
                {
                    Attributes = categoryDetail.Schema.Attributes
                        .Select(attribute => new CategorySchemaAttributeDto
                        {
                            Key = attribute.Key,
                            DisplayName = attribute.DisplayName,
                            ValueType = attribute.ValueType,
                            Unit = attribute.Unit,
                            IsRequired = string.Equals(attribute.Key, attributeKey, StringComparison.OrdinalIgnoreCase)
                                ? isRequired
                                : attribute.IsRequired,
                            ConflictSensitivity = attribute.ConflictSensitivity,
                            Description = attribute.Description
                        })
                        .ToArray()
                },
                cancellationToken);

            return new JsonResult(new
            {
                message = $"Saved {targetAttribute.DisplayName} as {(isRequired ? "required" : "optional")} for {updatedSchema.DisplayName}.",
                label = isRequired ? "required" : "optional"
            });
        }
        catch (AdminApiValidationException exception)
        {
            var message = exception.Errors
                .SelectMany(entry => entry.Value)
                .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))
                ?? "The schema update was rejected by the platform.";

            return new JsonResult(new { message })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to toggle required status for category schema attribute {AttributeKey} in category {CategoryKey}.", attributeKey, categoryKey);

            return new JsonResult(new { message = exception.Message })
            {
                StatusCode = StatusCodes.Status502BadGateway
            };
        }
    }

    public bool HasDiscoveryScaffold(SourceDto source)
    {
        return source.DiscoveryProfile.CategoryEntryPages.Count > 0
            || source.DiscoveryProfile.SitemapHints.Count > 0
            || source.DiscoveryProfile.ProductUrlPatterns.Count > 0
            || source.DiscoveryProfile.ListingUrlPatterns.Count > 0;
    }

    private static int EstimateSeedCount(SourceDto source, IReadOnlyList<string> categoryKeys)
    {
        var categorySeedCount = source.DiscoveryProfile.CategoryEntryPages
            .Where(entry => categoryKeys.Contains(entry.Key, StringComparer.OrdinalIgnoreCase))
            .Sum(entry => entry.Value.Count);

        return categorySeedCount + source.DiscoveryProfile.SitemapHints.Count;
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

            PopulateCategorySchemaEditor();

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

    private void PopulateCategorySchemaEditor()
    {
        if (SelectedCategory is null)
        {
            if (string.IsNullOrWhiteSpace(CategorySchema.CategoryKey))
            {
                CategorySchema = new ManageCategorySchemaInput();
            }

            return;
        }

        if (string.Equals(CategorySchema.CategoryKey, SelectedCategory.Metadata.CategoryKey, StringComparison.OrdinalIgnoreCase)
            && CategorySchema.Attributes.Count > 0)
        {
            return;
        }

        CategorySchema = new ManageCategorySchemaInput
        {
            CategoryKey = SelectedCategory.Metadata.CategoryKey,
            Attributes = SelectedCategory.Schema.Attributes
                .Select(attribute => new ManageCategorySchemaAttributeInput
                {
                    Key = attribute.Key,
                    DisplayName = attribute.DisplayName,
                    ValueType = attribute.ValueType,
                    Unit = attribute.Unit,
                    IsRequired = attribute.IsRequired,
                    ConflictSensitivity = attribute.ConflictSensitivity,
                    Description = attribute.Description
                })
                .ToList()
        };
    }

    private void PrepareCategorySchemaDraft()
    {
        CategorySchema.CategoryKey = NormaliseKey(CategorySchema.CategoryKey);

        if (HasMeaningfulNewAttribute(CategorySchema.NewAttribute))
        {
            var newAttribute = new ManageCategorySchemaAttributeInput
            {
                Key = CategorySchema.NewAttribute.Key,
                DisplayName = CategorySchema.NewAttribute.DisplayName,
                ValueType = CategorySchema.NewAttribute.ValueType,
                Unit = CategorySchema.NewAttribute.Unit,
                IsRequired = CategorySchema.NewAttribute.IsRequired,
                ConflictSensitivity = CategorySchema.NewAttribute.ConflictSensitivity,
                Description = CategorySchema.NewAttribute.Description
            };
            NormaliseAttribute(newAttribute);
            CategorySchema.Attributes.Add(newAttribute);
        }

        foreach (var attribute in CategorySchema.Attributes)
        {
            NormaliseAttribute(attribute);
        }

        CategorySchema.NewAttribute = new NewCategorySchemaAttributeInput();
    }

    private void ValidateCategorySchemaDraft()
    {
        if (CategorySchema.Attributes.Count == 0)
        {
            ModelState.AddModelError(nameof(CategorySchema.Attributes), "Track at least one attribute in the quality summary.");
            return;
        }

        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < CategorySchema.Attributes.Count; index++)
        {
            var attribute = CategorySchema.Attributes[index];
            if (string.IsNullOrWhiteSpace(attribute.Key))
            {
                ModelState.AddModelError($"{nameof(CategorySchema)}.{nameof(CategorySchema.Attributes)}[{index}].{nameof(ManageCategorySchemaAttributeInput.Key)}", "Attribute key is required.");
            }

            if (string.IsNullOrWhiteSpace(attribute.DisplayName))
            {
                ModelState.AddModelError($"{nameof(CategorySchema)}.{nameof(CategorySchema.Attributes)}[{index}].{nameof(ManageCategorySchemaAttributeInput.DisplayName)}", "Display name is required.");
            }

            if (!seenKeys.Add(attribute.Key))
            {
                ModelState.AddModelError($"{nameof(CategorySchema)}.{nameof(CategorySchema.Attributes)}[{index}].{nameof(ManageCategorySchemaAttributeInput.Key)}", $"Attribute key '{attribute.Key}' is duplicated.");
            }
        }
    }

    private static UpdateCategorySchemaRequest BuildUpdateCategorySchemaRequest(IEnumerable<ManageCategorySchemaAttributeInput> attributes)
    {
        return new UpdateCategorySchemaRequest
        {
            Attributes = attributes.Select(attribute => new CategorySchemaAttributeDto
            {
                Key = attribute.Key,
                DisplayName = attribute.DisplayName,
                ValueType = attribute.ValueType,
                Unit = attribute.Unit,
                IsRequired = attribute.IsRequired,
                ConflictSensitivity = attribute.ConflictSensitivity,
                Description = attribute.Description
            }).ToArray()
        };
    }

    private static bool HasMeaningfulNewAttribute(NewCategorySchemaAttributeInput attribute)
    {
        return !string.IsNullOrWhiteSpace(attribute.Key)
            || !string.IsNullOrWhiteSpace(attribute.DisplayName)
            || !string.IsNullOrWhiteSpace(attribute.Description)
            || !string.IsNullOrWhiteSpace(attribute.Unit);
    }

    private static void NormaliseAttribute(ManageCategorySchemaAttributeInput attribute)
    {
        attribute.Key = string.IsNullOrWhiteSpace(attribute.Key)
            ? NormaliseKey(attribute.DisplayName)
            : NormaliseKey(attribute.Key);
        attribute.DisplayName = string.IsNullOrWhiteSpace(attribute.DisplayName)
            ? ToDisplayName(attribute.Key)
            : attribute.DisplayName.Trim();
        attribute.ValueType = string.IsNullOrWhiteSpace(attribute.ValueType)
            ? "string"
            : attribute.ValueType.Trim().ToLowerInvariant();
        attribute.Unit = string.IsNullOrWhiteSpace(attribute.Unit) ? null : attribute.Unit.Trim();
        attribute.ConflictSensitivity = string.IsNullOrWhiteSpace(attribute.ConflictSensitivity)
            ? "Medium"
            : attribute.ConflictSensitivity.Trim();
        attribute.Description = string.IsNullOrWhiteSpace(attribute.Description)
            ? $"{attribute.DisplayName} captured during discovery."
            : attribute.Description.Trim();
    }

    private static string NormaliseKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Trim()
            .Replace("-", "_", StringComparison.Ordinal)
            .Replace(" ", "_", StringComparison.Ordinal)
            .ToLowerInvariant();
    }

    private static string ToDisplayName(string key)
    {
        return string.Join(' ', key
            .Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(segment => char.ToUpperInvariant(segment[0]) + segment[1..].ToLowerInvariant()));
    }

    private Dictionary<string, object?> BuildCurrentRouteValues(string categoryKey)
    {
        var selectedCategoryKeys = Request.Query["selectedCategory"]
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!selectedCategoryKeys.Contains(categoryKey, StringComparer.OrdinalIgnoreCase))
        {
            selectedCategoryKeys.Add(categoryKey);
        }

        return new Dictionary<string, object?>
        {
            ["category"] = categoryKey,
            ["selectedCategory"] = selectedCategoryKeys.ToArray()
        };
    }

    public sealed class QuickCrawlInput
    {
        [Required]
        public string CategoryKey { get; set; } = string.Empty;
    }

    public sealed class ManageCategorySchemaInput
    {
        public string CategoryKey { get; set; } = string.Empty;
        public List<ManageCategorySchemaAttributeInput> Attributes { get; set; } = [];
        public NewCategorySchemaAttributeInput NewAttribute { get; set; } = new();
    }

    public class ManageCategorySchemaAttributeInput
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ValueType { get; set; } = "string";
        public string? Unit { get; set; }
        public bool IsRequired { get; set; }
        public string ConflictSensitivity { get; set; } = "Medium";
        public string Description { get; set; } = string.Empty;
    }

    public sealed class NewCategorySchemaAttributeInput : ManageCategorySchemaAttributeInput
    {
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
