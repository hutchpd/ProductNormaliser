using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Models;

namespace ProductNormaliser.Web.Tests;

public sealed class CategorySelectorStateFactoryTests
{
    [Test]
    public void Create_GroupsCategoriesByFamily()
    {
        var state = CategorySelectorStateFactory.Create(
            CreateCategories(),
            ["tv"],
            inputName: "selectedCategory");

        Assert.Multiple(() =>
        {
            Assert.That(state.State, Is.EqualTo(CategorySelectorViewState.Ready));
            Assert.That(state.Families.Select(family => family.FamilyKey), Is.EqualTo(new[] { "audio", "display" }));
            Assert.That(state.Families.Single(family => family.FamilyKey == "display").Categories.Select(category => category.CategoryKey), Is.EqualTo(new[] { "monitor", "tv" }));
        });
    }

    [Test]
    public void ToggleCategorySelection_SelectsAndDeselectsCategory()
    {
        var selected = CategorySelectorStateFactory.ToggleCategorySelection(["tv"], "monitor", isSelected: true);
        var deselected = CategorySelectorStateFactory.ToggleCategorySelection(selected, "tv", isSelected: false);

        Assert.Multiple(() =>
        {
            Assert.That(selected, Is.EqualTo(new[] { "monitor", "tv" }));
            Assert.That(deselected, Is.EqualTo(new[] { "monitor" }));
        });
    }

    [Test]
    public void ApplyFamilySelection_SelectsOnlySelectableCategoriesInFamily()
    {
        var selected = CategorySelectorStateFactory.ApplyFamilySelection(CreateCategories(), [], "display", isSelected: true);

        Assert.That(selected, Is.EqualTo(new[] { "monitor", "tv" }));
    }

    [Test]
    public void Create_ReportsLoadingEmptyAndErrorStates()
    {
        var loading = CategorySelectorStateFactory.Create(null, [], "selectedCategory", isLoading: true);
        var empty = CategorySelectorStateFactory.Create([], [], "selectedCategory");
        var error = CategorySelectorStateFactory.Create([], [], "selectedCategory", errorMessage: "Failed to load categories.");

        Assert.Multiple(() =>
        {
            Assert.That(loading.State, Is.EqualTo(CategorySelectorViewState.Loading));
            Assert.That(empty.State, Is.EqualTo(CategorySelectorViewState.Empty));
            Assert.That(error.State, Is.EqualTo(CategorySelectorViewState.Error));
            Assert.That(error.ErrorMessage, Is.EqualTo("Failed to load categories."));
        });
    }

    private static IReadOnlyList<CategoryMetadataDto> CreateCategories()
    {
        return
        [
            new CategoryMetadataDto
            {
                CategoryKey = "tv",
                DisplayName = "TVs",
                FamilyKey = "display",
                FamilyDisplayName = "Display",
                CrawlSupportStatus = "Supported",
                SchemaCompletenessScore = 0.92m,
                IsEnabled = true
            },
            new CategoryMetadataDto
            {
                CategoryKey = "monitor",
                DisplayName = "Monitors",
                FamilyKey = "display",
                FamilyDisplayName = "Display",
                CrawlSupportStatus = "Experimental",
                SchemaCompletenessScore = 0.74m,
                IsEnabled = true
            },
            new CategoryMetadataDto
            {
                CategoryKey = "speaker",
                DisplayName = "Speakers",
                FamilyKey = "audio",
                FamilyDisplayName = "Audio",
                CrawlSupportStatus = "Disabled",
                SchemaCompletenessScore = 0.38m,
                IsEnabled = false
            }
        ];
    }
}