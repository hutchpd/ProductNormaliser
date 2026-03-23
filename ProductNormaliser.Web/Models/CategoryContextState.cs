using Microsoft.AspNetCore.Http;
using ProductNormaliser.Web.Contracts;

namespace ProductNormaliser.Web.Models;

public sealed class CategoryContextState
{
    public const string CookieName = "pn.category-context";

    public IReadOnlyList<CategoryMetadataDto> AvailableCategories { get; init; } = [];

    public IReadOnlyList<string> SelectedCategoryKeys { get; init; } = [];

    public IReadOnlyList<CategoryMetadataDto> SelectedCategories { get; init; } = [];

    public string? PrimaryCategoryKey { get; init; }

    public CategoryMetadataDto? PrimaryCategory { get; init; }

    public IReadOnlyList<string> InvalidCategoryKeys { get; init; } = [];

    public bool UsedPersistedSelection { get; init; }

    public bool HasSelection => SelectedCategoryKeys.Count > 0;

    public string SelectionSummary => SelectedCategories.Count switch
    {
        0 => "No category context",
        1 => SelectedCategories[0].DisplayName,
        _ => $"{SelectedCategories[0].DisplayName} +{SelectedCategories.Count - 1} more"
    };

    public string PersistedValue => string.Join(",", SelectedCategoryKeys);
}

public static class CategoryContextStateFactory
{
    public static CategoryContextState Resolve(
        IReadOnlyList<CategoryMetadataDto> categories,
        string? requestedPrimaryCategoryKey,
        IReadOnlyCollection<string>? requestedSelectedCategoryKeys,
        string? persistedSelection)
    {
        ArgumentNullException.ThrowIfNull(categories);

        var availableCategories = categories
            .OrderBy(category => category.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var availableKeys = new HashSet<string>(availableCategories.Select(category => category.CategoryKey), StringComparer.OrdinalIgnoreCase);

        var requestedSelected = NormalizeKeys(requestedSelectedCategoryKeys);
        var persistedSelected = ParsePersistedSelection(persistedSelection)
            .Where(availableKeys.Contains)
            .ToArray();
        var invalidRequested = requestedSelected
            .Where(key => !availableKeys.Contains(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var validRequestedSelected = requestedSelected
            .Where(availableKeys.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var requestedPrimary = string.IsNullOrWhiteSpace(requestedPrimaryCategoryKey)
            ? null
            : availableCategories.FirstOrDefault(category => string.Equals(category.CategoryKey, requestedPrimaryCategoryKey.Trim(), StringComparison.OrdinalIgnoreCase))?.CategoryKey;

        var selectedKeys = validRequestedSelected.Length > 0
            ? validRequestedSelected.ToList()
            : persistedSelected.Length > 0
                ? persistedSelected.ToList()
                : [];

        if (selectedKeys.Count == 0 && requestedPrimary is not null)
        {
            selectedKeys.Add(requestedPrimary);
        }

        var primaryCategoryKey = requestedPrimary
            ?? selectedKeys.FirstOrDefault()
            ?? availableCategories.FirstOrDefault()?.CategoryKey;

        if (primaryCategoryKey is not null && !selectedKeys.Contains(primaryCategoryKey, StringComparer.OrdinalIgnoreCase))
        {
            selectedKeys.Insert(0, primaryCategoryKey);
        }

        if (selectedKeys.Count == 0 && primaryCategoryKey is not null)
        {
            selectedKeys.Add(primaryCategoryKey);
        }

        var selectedCategories = selectedKeys
            .Select(key => availableCategories.First(category => string.Equals(category.CategoryKey, key, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var primaryCategory = primaryCategoryKey is null
            ? null
            : availableCategories.FirstOrDefault(category => string.Equals(category.CategoryKey, primaryCategoryKey, StringComparison.OrdinalIgnoreCase));

        return new CategoryContextState
        {
            AvailableCategories = availableCategories,
            SelectedCategoryKeys = selectedKeys,
            SelectedCategories = selectedCategories,
            PrimaryCategoryKey = primaryCategoryKey,
            PrimaryCategory = primaryCategory,
            InvalidCategoryKeys = invalidRequested,
            UsedPersistedSelection = validRequestedSelected.Length == 0 && persistedSelected.Length > 0
        };
    }

    public static void Persist(HttpResponse response, CategoryContextState state)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(state);

        if (!state.HasSelection)
        {
            response.Cookies.Delete(CategoryContextState.CookieName);
            return;
        }

        response.Cookies.Append(CategoryContextState.CookieName, state.PersistedValue, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = response.HttpContext.Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });
    }

    public static IReadOnlyList<string> ParsePersistedSelection(string? persistedSelection)
    {
        return NormalizeKeys(string.IsNullOrWhiteSpace(persistedSelection)
            ? []
            : persistedSelection.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static IReadOnlyList<string> NormalizeKeys(IEnumerable<string>? keys)
    {
        return (keys ?? [])
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}