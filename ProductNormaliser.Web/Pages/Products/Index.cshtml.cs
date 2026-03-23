using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Models;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Pages.Products;

public sealed class IndexModel(
    IProductNormaliserAdminApiClient adminApiClient,
    ILogger<IndexModel> logger) : PageModel
{
    private const string RoutePath = "/Products/Index";

    [BindProperty(SupportsGet = true, Name = "category")]
    public string? CategoryKey { get; set; }

    [BindProperty(SupportsGet = true, Name = "view")]
    public string? SavedViewId { get; set; }

    [BindProperty(SupportsGet = true, Name = "search")]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true, Name = "minSourceCount")]
    public int? MinSourceCount { get; set; }

    [BindProperty(SupportsGet = true, Name = "freshness")]
    public string? Freshness { get; set; }

    [BindProperty(SupportsGet = true, Name = "conflictStatus")]
    public string? ConflictStatus { get; set; }

    [BindProperty(SupportsGet = true, Name = "completeness")]
    public string? CompletenessStatus { get; set; }

    [BindProperty(SupportsGet = true, Name = "sort")]
    public string? Sort { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PageNumber { get; set; } = 1;

    [BindProperty]
    public string SaveViewName { get; set; } = string.Empty;

    [BindProperty]
    public string? SaveViewDescription { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public string? WorkflowMessage { get; private set; }

    public IReadOnlyList<CategoryMetadataDto> Categories { get; private set; } = [];

    public IReadOnlyList<SavedAnalystWorkflowCardModel> SavedWorkflows { get; private set; } = [];

    public ProductListResponseDto Products { get; private set; } = new() { Page = 1, PageSize = 12 };

    public int StaleProducts => Products.Items.Count(product => string.Equals(product.FreshnessStatus, "stale", StringComparison.OrdinalIgnoreCase));

    public int ProductsWithConflicts => Products.Items.Count(product => product.HasConflict);

    public decimal AverageCompleteness => Products.Items.Count == 0
        ? 0m
        : decimal.Round(Products.Items.Average(product => product.CompletenessScore), 2, MidpointRounding.AwayFromZero);

    public PaginationModel Pagination => new()
    {
        PagePath = "/Products/Index",
        CurrentPage = Products.Page,
        TotalPages = Products.TotalPages,
        TotalCount = Products.TotalCount,
        RouteValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["category"] = CategoryKey,
            ["search"] = Search,
            ["minSourceCount"] = MinSourceCount?.ToString(),
            ["freshness"] = Freshness,
            ["conflictStatus"] = ConflictStatus,
            ["completeness"] = CompletenessStatus,
            ["sort"] = Sort
        }
    };

    public PageHeroModel Hero => new()
    {
        Eyebrow = "Product Explorer",
        Title = "Search canonical products by quality signals",
        Description = "Use category, source coverage, freshness, conflict, and completeness filters to find products that need attention or products strong enough for downstream analysis.",
        Metrics =
        [
            new HeroMetricModel { Label = "Visible products", Value = Products.Items.Count.ToString() },
            new HeroMetricModel { Label = "Total matches", Value = Products.TotalCount.ToString() },
            new HeroMetricModel { Label = "Stale in view", Value = StaleProducts.ToString() },
            new HeroMetricModel { Label = "Avg completeness", Value = ProductExplorerPresentation.FormatPercent(AverageCompleteness) }
        ]
    };

    public StatusBadgeModel GetFreshnessBadge(ProductSummaryDto product) => ProductExplorerPresentation.GetFreshnessBadge(product.FreshnessStatus, product.FreshnessAgeDays);

    public StatusBadgeModel GetCompletenessBadge(ProductSummaryDto product) => ProductExplorerPresentation.GetCompletenessBadge(product.CompletenessScore, product.CompletenessStatus);

    public StatusBadgeModel GetConflictBadge(ProductSummaryDto product) => ProductExplorerPresentation.GetConflictBadge(product.HasConflict, product.ConflictAttributeCount);

    public string GetDetailUrl(ProductSummaryDto product)
    {
        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["productId"] = product.Id,
            ["category"] = CategoryKey,
            ["search"] = Search,
            ["returnPage"] = Math.Max(1, PageNumber).ToString(),
            ["minSourceCount"] = MinSourceCount?.ToString(),
            ["freshness"] = Freshness,
            ["conflictStatus"] = ConflictStatus,
            ["completeness"] = CompletenessStatus,
            ["sort"] = Sort
        };

        return QueryHelpers.AddQueryString("/Products/Details", query);
    }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var categoriesTask = adminApiClient.GetCategoriesAsync(cancellationToken);
            var workflowsTask = adminApiClient.GetAnalystWorkflowsAsync(routePath: RoutePath, cancellationToken: cancellationToken);
            await Task.WhenAll(categoriesTask, workflowsTask);

            Categories = InteractiveCategoryFilter.Apply(categoriesTask.Result);
            var workflowDefinitions = workflowsTask.Result;
            var restoredWorkflow = await ResolveSavedWorkflowAsync(workflowDefinitions, cancellationToken);
            ApplySavedWorkflow(restoredWorkflow);
            var categoryContext = CategoryContextStateFactory.Resolve(
                Categories,
                CategoryKey,
                null,
                PageContext?.HttpContext?.Request.Cookies[CategoryContextState.CookieName]);
            if (restoredWorkflow is not null && !string.IsNullOrWhiteSpace(restoredWorkflow.PrimaryCategoryKey) && !Categories.Any(category => string.Equals(category.CategoryKey, restoredWorkflow.PrimaryCategoryKey, StringComparison.OrdinalIgnoreCase)))
            {
                WorkflowMessage = $"Saved view '{restoredWorkflow.Name}' referenced missing category '{restoredWorkflow.PrimaryCategoryKey}'. Showing the default active category instead.";
            }

            CategoryKey = categoryContext.PrimaryCategoryKey;
            SavedWorkflows = AnalystWorkspacePresentation.BuildWorkflowCards(
                workflowDefinitions,
                Categories,
                SavedViewId,
                AnalystWorkspacePresentation.WorkflowTypeProductFilters,
                AnalystWorkspacePresentation.WorkflowTypeSelectedCategories);
            Products = await adminApiClient.GetProductsAsync(new ProductListQueryDto
            {
                CategoryKey = CategoryKey,
                Search = Search,
                MinSourceCount = MinSourceCount,
                Freshness = Freshness,
                ConflictStatus = ConflictStatus,
                CompletenessStatus = CompletenessStatus,
                Sort = Sort,
                Page = Math.Max(1, PageNumber),
                PageSize = 12
            }, cancellationToken);
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to load products page data.");
            ErrorMessage = exception.Message;
            Categories = [];
            Products = new ProductListResponseDto { Page = 1, PageSize = 12 };
        }
    }

    public async Task<IActionResult> OnPostSaveViewAsync(string workflowType, CancellationToken cancellationToken)
    {
        try
        {
            var workflow = await adminApiClient.SaveAnalystWorkflowAsync(BuildWorkflowRequest(workflowType), cancellationToken);
            StatusMessage = $"Saved analyst view '{workflow.Name}'.";
            return RedirectToPage(BuildRedirectRouteValues(workflow.Id));
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to save product workflow for {CategoryKey}.", CategoryKey);
            ErrorMessage = exception.Message;
            await OnGetAsync(cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteViewAsync(string workflowId, CancellationToken cancellationToken)
    {
        try
        {
            await adminApiClient.DeleteAnalystWorkflowAsync(workflowId, cancellationToken);
            StatusMessage = "Deleted analyst view.";
            return RedirectToPage(BuildRedirectRouteValues(null));
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to delete product workflow {WorkflowId}.", workflowId);
            ErrorMessage = exception.Message;
            await OnGetAsync(cancellationToken);
            return Page();
        }
    }

    private async Task<AnalystWorkflowDto?> ResolveSavedWorkflowAsync(IReadOnlyList<AnalystWorkflowDto> workflows, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(SavedViewId))
        {
            return null;
        }

        var workflow = workflows.FirstOrDefault(item => string.Equals(item.Id, SavedViewId, StringComparison.OrdinalIgnoreCase));
        if (workflow is not null)
        {
            return workflow;
        }

        workflow = await adminApiClient.GetAnalystWorkflowAsync(SavedViewId, cancellationToken);
        if (workflow is null)
        {
            WorkflowMessage = "Saved view was not found.";
        }

        return workflow;
    }

    private void ApplySavedWorkflow(AnalystWorkflowDto? workflow)
    {
        if (workflow is null)
        {
            return;
        }

        CategoryKey = workflow.State.TryGetValue("category", out var category) ? category : CategoryKey;
        Search = workflow.State.TryGetValue("search", out var search) ? search : Search;
        MinSourceCount = workflow.State.TryGetValue("minSourceCount", out var minSourceCount) && int.TryParse(minSourceCount, out var parsedMinSourceCount)
            ? parsedMinSourceCount
            : MinSourceCount;
        Freshness = workflow.State.TryGetValue("freshness", out var freshness) ? freshness : Freshness;
        ConflictStatus = workflow.State.TryGetValue("conflictStatus", out var conflictStatus) ? conflictStatus : ConflictStatus;
        CompletenessStatus = workflow.State.TryGetValue("completeness", out var completeness) ? completeness : CompletenessStatus;
        Sort = workflow.State.TryGetValue("sort", out var sort) ? sort : Sort;
        PageNumber = workflow.State.TryGetValue("page", out var page) && int.TryParse(page, out var parsedPage)
            ? Math.Max(1, parsedPage)
            : PageNumber;
    }

    private UpsertAnalystWorkflowRequest BuildWorkflowRequest(string workflowType)
    {
        return new UpsertAnalystWorkflowRequest
        {
            Id = SavedViewId,
            Name = string.IsNullOrWhiteSpace(SaveViewName) ? $"{(string.IsNullOrWhiteSpace(CategoryKey) ? "All categories" : CategoryKey)} product view" : SaveViewName.Trim(),
            Description = string.IsNullOrWhiteSpace(SaveViewDescription) ? null : SaveViewDescription.Trim(),
            WorkflowType = workflowType,
            RoutePath = RoutePath,
            PrimaryCategoryKey = CategoryKey,
            SelectedCategoryKeys = string.IsNullOrWhiteSpace(CategoryKey) ? [] : [CategoryKey],
            State = BuildCurrentState()
        };
    }

    private Dictionary<string, string> BuildCurrentState()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["category"] = CategoryKey ?? string.Empty,
            ["search"] = Search ?? string.Empty,
            ["minSourceCount"] = MinSourceCount?.ToString() ?? string.Empty,
            ["freshness"] = Freshness ?? string.Empty,
            ["conflictStatus"] = ConflictStatus ?? string.Empty,
            ["completeness"] = CompletenessStatus ?? string.Empty,
            ["sort"] = Sort ?? string.Empty,
            ["page"] = Math.Max(1, PageNumber).ToString()
        };
    }

    private object BuildRedirectRouteValues(string? workflowId)
    {
        return new
        {
            category = CategoryKey,
            search = Search,
            minSourceCount = MinSourceCount,
            freshness = Freshness,
            conflictStatus = ConflictStatus,
            completeness = CompletenessStatus,
            sort = Sort,
            page = PageNumber,
            view = workflowId
        };
    }
}