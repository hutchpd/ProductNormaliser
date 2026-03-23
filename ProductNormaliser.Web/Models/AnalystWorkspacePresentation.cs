using Microsoft.AspNetCore.WebUtilities;
using ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Models;

public static class AnalystWorkspacePresentation
{
    public const string WorkflowTypeSelectedCategories = "selected_categories";
    public const string WorkflowTypeProductFilters = "product_filters";
    public const string WorkflowTypeConflictReviewQueue = "conflict_review_queue";
    public const string WorkflowTypeSourceReviewQueue = "source_review_queue";

    public static IReadOnlyList<SavedAnalystWorkflowCardModel> BuildWorkflowCards(
        IReadOnlyList<AnalystWorkflowDto> workflows,
        IReadOnlyList<CategoryMetadataDto> categories,
        string? currentWorkflowId,
        params string[] allowedTypes)
    {
        var allowed = allowedTypes.Length == 0
            ? null
            : allowedTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var categoryLookup = categories.ToDictionary(category => category.CategoryKey, StringComparer.OrdinalIgnoreCase);

        return workflows
            .Where(workflow => allowed is null || allowed.Contains(workflow.WorkflowType))
            .OrderByDescending(workflow => workflow.UpdatedUtc)
            .ThenBy(workflow => workflow.Name, StringComparer.OrdinalIgnoreCase)
            .Select(workflow =>
            {
                var missingCategoryKeys = workflow.SelectedCategoryKeys
                    .Append(workflow.PrimaryCategoryKey ?? string.Empty)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Where(categoryKey => !categoryLookup.ContainsKey(categoryKey))
                    .ToArray();
                var primaryCategoryLabel = string.IsNullOrWhiteSpace(workflow.PrimaryCategoryKey)
                    ? "Any category"
                    : categoryLookup.TryGetValue(workflow.PrimaryCategoryKey, out var category)
                        ? category.DisplayName
                        : $"Missing: {workflow.PrimaryCategoryKey}";

                return new SavedAnalystWorkflowCardModel(
                    workflow.Id,
                    workflow.Name,
                    workflow.Description,
                    GetWorkflowTypeLabel(workflow.WorkflowType),
                    primaryCategoryLabel,
                    BuildSummary(workflow),
                    BuildRestoreUrl(workflow),
                    missingCategoryKeys.Length > 0,
                    missingCategoryKeys.Length > 0 ? $"Missing categories: {string.Join(", ", missingCategoryKeys)}" : "Ready to restore",
                    missingCategoryKeys.Length > 0 ? "danger" : "completed",
                    string.Equals(currentWorkflowId, workflow.Id, StringComparison.OrdinalIgnoreCase));
            })
            .ToArray();
    }

    public static string BuildRestoreUrl(AnalystWorkflowDto workflow)
    {
        var query = workflow.State.ToDictionary(entry => entry.Key, entry => (string?)entry.Value, StringComparer.OrdinalIgnoreCase);
        query["view"] = workflow.Id;
        return QueryHelpers.AddQueryString(workflow.RoutePath, query);
    }

    public static string GetWorkflowTypeLabel(string workflowType)
    {
        return workflowType switch
        {
            WorkflowTypeSelectedCategories => "Category watchlist",
            WorkflowTypeProductFilters => "Product filters",
            WorkflowTypeConflictReviewQueue => "Conflict review queue",
            WorkflowTypeSourceReviewQueue => "Source review queue",
            _ => workflowType
        };
    }

    private static string BuildSummary(AnalystWorkflowDto workflow)
    {
        var parts = new List<string>();
        if (workflow.State.TryGetValue("search", out var search) && !string.IsNullOrWhiteSpace(search))
        {
            parts.Add($"Search: {search}");
        }

        if (workflow.State.TryGetValue("source", out var source) && !string.IsNullOrWhiteSpace(source))
        {
            parts.Add($"Source: {source}");
        }

        if (workflow.State.TryGetValue("range", out var range) && !string.IsNullOrWhiteSpace(range))
        {
            parts.Add($"Window: {range}d");
        }

        if (workflow.State.TryGetValue("freshness", out var freshness) && !string.IsNullOrWhiteSpace(freshness))
        {
            parts.Add($"Freshness: {freshness}");
        }

        if (workflow.State.TryGetValue("conflictStatus", out var conflictStatus) && !string.IsNullOrWhiteSpace(conflictStatus))
        {
            parts.Add($"Conflicts: {conflictStatus}");
        }

        if (workflow.State.TryGetValue("enabled", out var enabled) && !string.IsNullOrWhiteSpace(enabled))
        {
            parts.Add($"Enabled: {enabled}");
        }

        return parts.Count == 0 ? "Saved route state" : string.Join(" • ", parts.Take(3));
    }
}

public sealed record SavedAnalystWorkflowCardModel(
    string Id,
    string Name,
    string? Description,
    string WorkflowTypeLabel,
    string PrimaryCategoryLabel,
    string Summary,
    string RestoreUrl,
    bool HasMissingCategory,
    string StatusText,
    string StatusTone,
    bool IsCurrent);