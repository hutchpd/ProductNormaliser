using ProductNormaliser.Application.Categories;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Tests;

public sealed class SourceManagementServiceTests
{
    [Test]
    public async Task RegisterAsync_CreatesNormalisedSource()
    {
        var store = new FakeCrawlSourceStore();
        var service = new SourceManagementService(store, new FakeCategoryMetadataService(CreateCategory("tv"), CreateCategory("refrigerator")));

        var result = await service.RegisterAsync(new CrawlSourceRegistration
        {
            SourceId = "Retailer One",
            DisplayName = "Retailer One",
            BaseUrl = "https://www.retailer-one.example/",
            SupportedCategoryKeys = ["tv", "refrigerator"]
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo("retailer_one"));
            Assert.That(result.Host, Is.EqualTo("www.retailer-one.example"));
            Assert.That(result.SupportedCategoryKeys, Is.EqualTo(new[] { "refrigerator", "tv" }));
            Assert.That(store.Items, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void RegisterAsync_RejectsUnknownCategories()
    {
        var service = new SourceManagementService(new FakeCrawlSourceStore(), new FakeCategoryMetadataService(CreateCategory("tv")));

        var action = async () => await service.RegisterAsync(new CrawlSourceRegistration
        {
            SourceId = "alpha",
            DisplayName = "Alpha",
            BaseUrl = "https://alpha.example",
            SupportedCategoryKeys = ["tv", "smartwatch"]
        });

        Assert.That(action, Throws.ArgumentException.With.Message.Contain("Unknown category keys"));
    }

    [Test]
    public async Task AssignCategoriesAsync_UpdatesSupportedCategories()
    {
        var store = new FakeCrawlSourceStore(new CrawlSource
        {
            Id = "alpha",
            DisplayName = "Alpha",
            BaseUrl = "https://alpha.example",
            Host = "alpha.example",
            IsEnabled = true,
            SupportedCategoryKeys = ["tv"],
            ThrottlingPolicy = new SourceThrottlingPolicy(),
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        });
        var service = new SourceManagementService(store, new FakeCategoryMetadataService(CreateCategory("tv"), CreateCategory("refrigerator")));

        var result = await service.AssignCategoriesAsync("alpha", ["refrigerator"]);

        Assert.That(result.SupportedCategoryKeys, Is.EqualTo(new[] { "refrigerator" }));
    }

    [Test]
    public void SetThrottlingAsync_RejectsInvalidPolicy()
    {
        var store = new FakeCrawlSourceStore(new CrawlSource
        {
            Id = "alpha",
            DisplayName = "Alpha",
            BaseUrl = "https://alpha.example",
            Host = "alpha.example",
            IsEnabled = true,
            ThrottlingPolicy = new SourceThrottlingPolicy(),
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        });
        var service = new SourceManagementService(store, new FakeCategoryMetadataService(CreateCategory("tv")));

        var action = async () => await service.SetThrottlingAsync("alpha", new SourceThrottlingPolicy
        {
            MinDelayMs = 2000,
            MaxDelayMs = 1000,
            MaxConcurrentRequests = 1,
            RequestsPerMinute = 10,
            RespectRobotsTxt = true
        });

        Assert.That(action, Throws.ArgumentException.With.Message.Contain("Maximum delay"));
    }

    [Test]
    public async Task EnableAndDisableAsync_ToggleSourceState()
    {
        var store = new FakeCrawlSourceStore(new CrawlSource
        {
            Id = "alpha",
            DisplayName = "Alpha",
            BaseUrl = "https://alpha.example",
            Host = "alpha.example",
            IsEnabled = false,
            ThrottlingPolicy = new SourceThrottlingPolicy(),
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        });
        var service = new SourceManagementService(store, new FakeCategoryMetadataService(CreateCategory("tv")));

        var enabled = await service.EnableAsync("alpha");
        var enabledState = enabled.IsEnabled;
        var disabled = await service.DisableAsync("alpha");

        Assert.Multiple(() =>
        {
            Assert.That(enabledState, Is.True);
            Assert.That(disabled.IsEnabled, Is.False);
        });
    }

    private static CategoryMetadata CreateCategory(string key)
    {
        return new CategoryMetadata
        {
            CategoryKey = key,
            DisplayName = key,
            FamilyKey = "family",
            FamilyDisplayName = "Family",
            IconKey = key,
            CrawlSupportStatus = CrawlSupportStatus.Supported,
            SchemaCompletenessScore = 1m,
            IsEnabled = true
        };
    }

    private sealed class FakeCrawlSourceStore(params CrawlSource[] sources) : ICrawlSourceStore
    {
        public List<CrawlSource> Items { get; } = sources.ToList();

        public Task<IReadOnlyList<CrawlSource>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<CrawlSource>>(Items.ToArray());
        }

        public Task<CrawlSource?> GetAsync(string sourceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items.FirstOrDefault(source => string.Equals(source.Id, sourceId, StringComparison.OrdinalIgnoreCase)));
        }

        public Task UpsertAsync(CrawlSource source, CancellationToken cancellationToken = default)
        {
            Items.RemoveAll(existing => string.Equals(existing.Id, source.Id, StringComparison.OrdinalIgnoreCase));
            Items.Add(source);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCategoryMetadataService(params CategoryMetadata[] categories) : ICategoryMetadataService
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