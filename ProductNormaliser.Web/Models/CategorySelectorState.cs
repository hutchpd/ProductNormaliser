using ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Models;

public enum CategorySelectorViewState
{
    Loading,
    Ready,
    Empty,
    Error
}

public sealed class CategorySelectorState
{
    public string InputName { get; init; } = string.Empty;
    public CategorySelectorViewState State { get; init; }
    public string EmptyMessage { get; init; } = "No categories are available.";
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<string> SelectedCategoryKeys { get; init; } = [];
    public IReadOnlyList<CategorySelectorFamilyModel> Families { get; init; } = [];
    public IReadOnlyList<CategorySelectorCategoryModel> SelectedCategories { get; init; } = [];

    public bool HasSelection => SelectedCategories.Count > 0;

    public int SelectedCount => SelectedCategories.Count;

    public int SelectedFamilyCount => SelectedCategories
        .Select(category => category.FamilyKey)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    public decimal AverageSchemaCompletenessScore => SelectedCategories.Count == 0
        ? 0m
        : decimal.Round(SelectedCategories.Average(category => category.SchemaCompletenessScore) * 100m, 1, MidpointRounding.AwayFromZero);
}

public sealed class CategorySelectorFamilyModel
{
    public string FamilyKey { get; init; } = string.Empty;
    public string FamilyDisplayName { get; init; } = string.Empty;
    public IReadOnlyList<CategorySelectorCategoryModel> Categories { get; init; } = [];
    public int SelectableCount { get; init; }
    public int SelectedCount { get; init; }
    public bool AllSelected { get; init; }
}

public sealed class CategorySelectorCategoryModel
{
    public string CategoryKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string FamilyKey { get; init; } = string.Empty;
    public string FamilyDisplayName { get; init; } = string.Empty;
    public string IconKey { get; init; } = string.Empty;
    public string CrawlSupportStatus { get; init; } = string.Empty;
    public decimal SchemaCompletenessScore { get; init; }
    public bool IsEnabled { get; init; }
    public bool IsSelected { get; init; }

    public bool IsSelectable => IsEnabled && !string.Equals(CrawlSupportStatus, "Disabled", StringComparison.OrdinalIgnoreCase);

    public string SchemaCompletenessLabel => $"{decimal.Round(SchemaCompletenessScore * 100m, 0, MidpointRounding.AwayFromZero):0}%";

    public string SelectionTone => IsEnabled ? "completed" : "danger";

    public string CrawlSupportTone => CrawlSupportStatus.ToLowerInvariant() switch
    {
        "supported" => "completed",
        "experimental" => "warning",
        "planned" => "pending",
        "disabled" => "danger",
        _ => "neutral"
    };

    public static CategorySelectorCategoryModel FromMetadata(CategoryMetadataDto category, bool isSelected)
    {
        return new CategorySelectorCategoryModel
        {
            CategoryKey = category.CategoryKey,
            DisplayName = category.DisplayName,
            FamilyKey = category.FamilyKey,
            FamilyDisplayName = category.FamilyDisplayName,
            IconKey = category.IconKey,
            CrawlSupportStatus = category.CrawlSupportStatus,
            SchemaCompletenessScore = category.SchemaCompletenessScore,
            IsEnabled = category.IsEnabled,
            IsSelected = isSelected
        };
    }
}