using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProductNormaliser.Web.Contracts;
using ProductNormaliser.Web.Models;
using ProductNormaliser.Web.Services;

namespace ProductNormaliser.Web.Pages.Categories;

public sealed class IndexModel(
    IProductNormaliserAdminApiClient adminApiClient,
    ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "selectedCategory")]
    public List<string> SelectedCategoryKeys { get; set; } = [];

    public string? ErrorMessage { get; private set; }

    public IReadOnlyList<CategoryMetadataDto> Categories { get; private set; } = [];

    public CategorySelectorState Selector { get; private set; } = CategorySelectorStateFactory.Create([], [], "selectedCategory", isLoading: true);

    public PageHeroModel Hero => new()
    {
        Eyebrow = "Category Selection",
        Title = "Choose crawl categories by family",
        Description = "Operators can browse grouped category metadata, select a whole family in one action, and move straight into crawl launch with the chosen commodities already staged.",
        Metrics =
        [
            new HeroMetricModel { Label = "All categories", Value = Categories.Count.ToString() },
            new HeroMetricModel { Label = "Enabled", Value = Categories.Count(category => category.IsEnabled).ToString() },
            new HeroMetricModel { Label = "Families", Value = Categories.Select(category => category.FamilyKey).Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString() }
        ]
    };

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            Categories = InteractiveCategoryFilter.Apply(await adminApiClient.GetCategoriesAsync(cancellationToken));
            var categoryContext = CategoryContextStateFactory.Resolve(
                Categories,
                null,
                SelectedCategoryKeys,
                PageContext?.HttpContext?.Request.Cookies[CategoryContextState.CookieName]);
            SelectedCategoryKeys = categoryContext.SelectedCategoryKeys.ToList();
            Selector = CategorySelectorStateFactory.Create(
                Categories,
                SelectedCategoryKeys,
                inputName: "selectedCategory",
                emptyMessage: "No categories are available to select yet.");
        }
        catch (AdminApiException exception)
        {
            logger.LogWarning(exception, "Failed to load categories page data.");
            ErrorMessage = exception.Message;
            Categories = [];
            Selector = CategorySelectorStateFactory.Create(
                Categories,
                SelectedCategoryKeys,
                inputName: "selectedCategory",
                errorMessage: ErrorMessage);
        }
    }
}