using ProductNormaliser.Application.Categories;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.CategorySchema)]
public sealed class CategoryManagementServiceTests
{
    [Test]
    public async Task ListFamiliesAsync_GroupsCategoriesByFamily()
    {
        var service = new CategoryManagementService(
            new FakeCategoryMetadataService(
            [
                CreateCategory("tv", "TVs", "display", "Display", CrawlSupportStatus.Supported, true),
                CreateCategory("monitor", "Monitors", "display", "Display", CrawlSupportStatus.Supported, true),
                CreateCategory("laptop", "Laptops", "computing", "Computing", CrawlSupportStatus.Supported, true)
            ]),
            new CategorySchemaRegistry([new TvCategorySchemaProvider(), new MonitorCategorySchemaProvider(), new LaptopCategorySchemaProvider()]));

        var result = await service.ListFamiliesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].FamilyKey, Is.EqualTo("computing"));
            Assert.That(result[1].Categories.Select(category => category.CategoryKey), Is.EqualTo(new[] { "monitor", "tv" }));
        });
    }

    [Test]
    public async Task ListEnabledAsync_ReturnsOnlyEnabledCrawlableCategories()
    {
        var service = new CategoryManagementService(
            new FakeCategoryMetadataService(
            [
                CreateCategory("tv", "TVs", "display", "Display", CrawlSupportStatus.Supported, true),
                CreateCategory("monitor", "Monitors", "display", "Display", CrawlSupportStatus.Supported, true),
                CreateCategory("laptop", "Laptops", "computing", "Computing", CrawlSupportStatus.Supported, true),
                CreateCategory("refrigerator", "Refrigerators", "kitchen_appliances", "Kitchen Appliances", CrawlSupportStatus.Disabled, true)
            ]),
            new CategorySchemaRegistry([new TvCategorySchemaProvider(), new MonitorCategorySchemaProvider(), new LaptopCategorySchemaProvider(), new RefrigeratorCategorySchemaProvider()]));

        var result = await service.ListEnabledAsync();

        Assert.That(result.Select(category => category.CategoryKey), Is.EqualTo(new[] { "laptop", "monitor", "tv" }));
    }

    [Test]
    public async Task GetSchemaAsync_ReturnsNullForUnknownCategory()
    {
        var service = new CategoryManagementService(
            new FakeCategoryMetadataService(),
            new CategorySchemaRegistry([new TvCategorySchemaProvider()]));

        var result = await service.GetSchemaAsync("unknown-category");

        Assert.That(result, Is.Null);
    }

    private static CategoryMetadata CreateCategory(string categoryKey, string displayName, string familyKey, string familyDisplayName, CrawlSupportStatus crawlSupportStatus, bool isEnabled)
    {
        return new CategoryMetadata
        {
            CategoryKey = categoryKey,
            DisplayName = displayName,
            FamilyKey = familyKey,
            FamilyDisplayName = familyDisplayName,
            IconKey = categoryKey,
            CrawlSupportStatus = crawlSupportStatus,
            SchemaCompletenessScore = 0.5m,
            IsEnabled = isEnabled
        };
    }

    private sealed class FakeCategoryMetadataService(params IReadOnlyList<CategoryMetadata> categories) : ICategoryMetadataService
    {
        private readonly List<CategoryMetadata> items = categories.ToList();

        public Task EnsureDefaultsAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CategoryMetadata>> ListAsync(bool enabledOnly = false, CancellationToken cancellationToken = default)
        {
            var result = enabledOnly ? items.Where(category => category.IsEnabled).ToArray() : items.ToArray();
            return Task.FromResult<IReadOnlyList<CategoryMetadata>>(result);
        }

        public Task<CategoryMetadata?> GetAsync(string categoryKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(items.FirstOrDefault(category => string.Equals(category.CategoryKey, categoryKey, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<CategoryMetadata> UpsertAsync(CategoryMetadata categoryMetadata, CancellationToken cancellationToken = default)
        {
            items.RemoveAll(category => string.Equals(category.CategoryKey, categoryMetadata.CategoryKey, StringComparison.OrdinalIgnoreCase));
            items.Add(categoryMetadata);
            return Task.FromResult(categoryMetadata);
        }
    }
}