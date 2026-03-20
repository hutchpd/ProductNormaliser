using ProductNormaliser.Application.Categories;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Application.Tests;

public sealed class CategoryMetadataServiceTests
{
    [Test]
    public async Task ListAsync_SeedsAndReturnsDefaultCategories()
    {
        var store = new InMemoryCategoryMetadataStore();
        var service = new CategoryMetadataService(store);

        var categories = await service.ListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(categories, Has.Count.EqualTo(12));
            Assert.That(categories[0].FamilyDisplayName, Is.Not.Empty);
            Assert.That(store.ListAsync().GetAwaiter().GetResult(), Has.Count.EqualTo(12));
        });
    }

    [Test]
    public async Task UpsertAsync_AllowsUiToAmendCategoryMetadata()
    {
        var store = new InMemoryCategoryMetadataStore();
        var service = new CategoryMetadataService(store);

        await service.EnsureDefaultsAsync();
        var updated = await service.UpsertAsync(new CategoryMetadata
        {
            CategoryKey = "Monitor",
            DisplayName = "Monitors",
            FamilyKey = "Display",
            FamilyDisplayName = "Display",
            IconKey = "Desktop-Monitor",
            CrawlSupportStatus = CrawlSupportStatus.Experimental,
            SchemaCompletenessScore = 0.65m,
            IsEnabled = true
        });

        var retrieved = await service.GetAsync("monitor");

        Assert.Multiple(() =>
        {
            Assert.That(updated.CategoryKey, Is.EqualTo("monitor"));
            Assert.That(updated.IconKey, Is.EqualTo("desktop_monitor"));
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved!.CrawlSupportStatus, Is.EqualTo(CrawlSupportStatus.Experimental));
            Assert.That(retrieved.SchemaCompletenessScore, Is.EqualTo(0.65m));
            Assert.That(retrieved.IsEnabled, Is.True);
        });
    }

    private sealed class InMemoryCategoryMetadataStore : ICategoryMetadataStore
    {
        private readonly Dictionary<string, CategoryMetadata> categories = new(StringComparer.OrdinalIgnoreCase);

        public Task<CategoryMetadata?> GetAsync(string categoryKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(categories.TryGetValue(categoryKey, out var category) ? Clone(category) : null);
        }

        public Task<IReadOnlyList<CategoryMetadata>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<CategoryMetadata>>(categories.Values.Select(Clone).ToArray());
        }

        public Task UpsertAsync(CategoryMetadata categoryMetadata, CancellationToken cancellationToken = default)
        {
            categories[categoryMetadata.CategoryKey] = Clone(categoryMetadata);
            return Task.CompletedTask;
        }

        private static CategoryMetadata Clone(CategoryMetadata category)
        {
            return new CategoryMetadata
            {
                CategoryKey = category.CategoryKey,
                DisplayName = category.DisplayName,
                FamilyKey = category.FamilyKey,
                FamilyDisplayName = category.FamilyDisplayName,
                IconKey = category.IconKey,
                CrawlSupportStatus = category.CrawlSupportStatus,
                SchemaCompletenessScore = category.SchemaCompletenessScore,
                IsEnabled = category.IsEnabled
            };
        }
    }
}