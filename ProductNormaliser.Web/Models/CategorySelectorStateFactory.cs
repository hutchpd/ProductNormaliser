using ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Models;

public static class CategorySelectorStateFactory
{
    public static CategorySelectorState Create(
        IReadOnlyList<CategoryMetadataDto>? categories,
        IEnumerable<string>? selectedCategoryKeys,
        string inputName,
        string? errorMessage = null,
        bool isLoading = false,
        string? emptyMessage = null)
    {
        if (isLoading)
        {
            return new CategorySelectorState
            {
                InputName = inputName,
                State = CategorySelectorViewState.Loading,
                EmptyMessage = emptyMessage ?? "Loading categories..."
            };
        }

        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            return new CategorySelectorState
            {
                InputName = inputName,
                State = CategorySelectorViewState.Error,
                ErrorMessage = errorMessage,
                EmptyMessage = emptyMessage ?? "Categories could not be loaded."
            };
        }

        categories ??= [];
        var normalizedSelection = NormalizeSelection(categories, selectedCategoryKeys);

        if (categories.Count == 0)
        {
            return new CategorySelectorState
            {
                InputName = inputName,
                State = CategorySelectorViewState.Empty,
                EmptyMessage = emptyMessage ?? "No categories are configured yet.",
                SelectedCategoryKeys = normalizedSelection
            };
        }

            var families = categories
                .GroupBy(category => category.FamilyKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                    var familyDisplayName = group.First().FamilyDisplayName;
                var categoryModels = group
                    .OrderBy(category => category.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(category => CategorySelectorCategoryModel.FromMetadata(category, normalizedSelection.Contains(category.CategoryKey, StringComparer.OrdinalIgnoreCase)))
                    .ToArray();

                var selectableCount = categoryModels.Count(category => category.IsSelectable);
                var selectedCount = categoryModels.Count(category => category.IsSelectable && category.IsSelected);

                return new CategorySelectorFamilyModel
                {
                        FamilyKey = group.Key,
                        FamilyDisplayName = familyDisplayName,
                    Categories = categoryModels,
                    SelectableCount = selectableCount,
                    SelectedCount = selectedCount,
                    AllSelected = selectableCount > 0 && selectedCount == selectableCount
                };
            })
            .OrderBy(family => family.FamilyDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CategorySelectorState
        {
            InputName = inputName,
            State = CategorySelectorViewState.Ready,
            EmptyMessage = emptyMessage ?? "No categories are available.",
            SelectedCategoryKeys = normalizedSelection,
            Families = families,
            SelectedCategories = families
                .SelectMany(family => family.Categories)
                .Where(category => category.IsSelected)
                .OrderBy(category => category.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    public static IReadOnlyList<string> NormalizeSelection(IReadOnlyList<CategoryMetadataDto>? categories, IEnumerable<string>? selectedCategoryKeys)
    {
        categories ??= [];
        if (selectedCategoryKeys is null)
        {
            return [];
        }

        var knownKeys = categories.Select(category => category.CategoryKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return selectedCategoryKeys
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Where(knownKeys.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> ToggleCategorySelection(IEnumerable<string> selectedCategoryKeys, string categoryKey, bool isSelected)
    {
        var currentSelection = selectedCategoryKeys
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (isSelected)
        {
            currentSelection.Add(categoryKey);
        }
        else
        {
            currentSelection.Remove(categoryKey);
        }

        return currentSelection.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static IReadOnlyList<string> ApplyFamilySelection(
        IReadOnlyList<CategoryMetadataDto> categories,
        IEnumerable<string> selectedCategoryKeys,
        string familyKey,
        bool isSelected)
    {
        var currentSelection = selectedCategoryKeys
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var familyCategoryKeys = categories
            .Where(category => string.Equals(category.FamilyKey, familyKey, StringComparison.OrdinalIgnoreCase))
            .Where(category => category.IsEnabled && !string.Equals(category.CrawlSupportStatus, "Disabled", StringComparison.OrdinalIgnoreCase))
            .Select(category => category.CategoryKey)
            .ToArray();

        foreach (var categoryKey in familyCategoryKeys)
        {
            if (isSelected)
            {
                currentSelection.Add(categoryKey);
            }
            else
            {
                currentSelection.Remove(categoryKey);
            }
        }

        return currentSelection.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}