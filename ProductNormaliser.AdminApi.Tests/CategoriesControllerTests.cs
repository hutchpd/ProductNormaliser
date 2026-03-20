using Microsoft.AspNetCore.Mvc;
using ProductNormaliser.AdminApi.Contracts;
using ProductNormaliser.AdminApi.Controllers;
using ProductNormaliser.Application.Categories;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.AdminApi.Tests;

public sealed class CategoriesControllerTests
{
    [Test]
    public async Task GetCategories_ReturnsMappedDtos()
    {
        var service = new FakeCategoryManagementService(
        [
            new CategoryMetadata
            {
                CategoryKey = "tv",
                DisplayName = "TVs",
                FamilyKey = "display",
                FamilyDisplayName = "Display",
                IconKey = "tv",
                CrawlSupportStatus = CrawlSupportStatus.Supported,
                SchemaCompletenessScore = 1.0m,
                IsEnabled = true
            }
        ]);
        var controller = new CategoriesController(service);

        var result = await controller.GetCategories();

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var payload = ok!.Value as CategoryMetadataDto[];
        Assert.That(payload, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(payload!, Has.Length.EqualTo(1));
            Assert.That(payload[0].CategoryKey, Is.EqualTo("tv"));
            Assert.That(payload[0].CrawlSupportStatus, Is.EqualTo("Supported"));
        });
    }

    [Test]
    public async Task GetFamilies_ReturnsFamiliesAndTheirCategories()
    {
        var service = new FakeCategoryManagementService(
        [
            CreateCategory("tv", "TVs", "display", "Display", CrawlSupportStatus.Supported, true),
            CreateCategory("monitor", "Monitors", "display", "Display", CrawlSupportStatus.Experimental, true),
            CreateCategory("laptop", "Laptops", "computing", "Computing", CrawlSupportStatus.Planned, false)
        ]);
        var controller = new CategoriesController(service);

        var result = await controller.GetFamilies();

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var payload = ok!.Value as CategoryFamilyDto[];
        Assert.That(payload, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(payload!, Has.Length.EqualTo(2));
            Assert.That(payload[0].FamilyKey, Is.EqualTo("computing"));
            Assert.That(payload[1].FamilyKey, Is.EqualTo("display"));
            Assert.That(payload[1].Categories.Select(category => category.CategoryKey), Is.EqualTo(new[] { "monitor", "tv" }));
        });
    }

    [Test]
    public async Task GetCategory_ReturnsNotFoundForUnknownCategory()
    {
        var controller = new CategoriesController(new FakeCategoryManagementService());

        var result = await controller.GetCategory("unknown-category");

        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task GetCategoryDetail_ReturnsCombinedMetadataAndSchema()
    {
        var service = new FakeCategoryManagementService(
            [CreateCategory("tv", "TVs", "display", "Display", CrawlSupportStatus.Supported, true)],
            new Dictionary<string, CategorySchema>(StringComparer.OrdinalIgnoreCase)
            {
                ["tv"] = new()
                {
                    CategoryKey = "tv",
                    DisplayName = "Televisions",
                    Attributes =
                    [
                        new CanonicalAttributeDefinition
                        {
                            Key = "screen_size_inch",
                            DisplayName = "Screen Size",
                            ValueType = "decimal",
                            Unit = "inch",
                            Description = "Nominal display size."
                        }
                    ]
                }
            });
        var controller = new CategoriesController(service);

        var result = await controller.GetCategoryDetail("tv");

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var payload = ok!.Value as CategoryDetailDto;
        Assert.That(payload, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(payload!.Metadata.CategoryKey, Is.EqualTo("tv"));
            Assert.That(payload.Schema.CategoryKey, Is.EqualTo("tv"));
            Assert.That(payload.Schema.Attributes[0].Key, Is.EqualTo("screen_size_inch"));
        });
    }

    [Test]
    public async Task GetCategoryDetail_ReturnsNotFoundForUnknownCategory()
    {
        var controller = new CategoriesController(new FakeCategoryManagementService());

        var result = await controller.GetCategoryDetail("unknown-category");

        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task GetCategorySchema_ReturnsSchemaMetadataForKnownCategory()
    {
        var service = new FakeCategoryManagementService(
            [CreateCategory("tv", "TVs", "display", "Display", CrawlSupportStatus.Supported, true)],
            new Dictionary<string, CategorySchema>(StringComparer.OrdinalIgnoreCase)
            {
                ["tv"] = new()
                {
                    CategoryKey = "tv",
                    DisplayName = "Televisions",
                    Attributes =
                    [
                        new CanonicalAttributeDefinition
                        {
                            Key = "screen_size_inch",
                            DisplayName = "Screen Size",
                            ValueType = "decimal",
                            Unit = "inch",
                            IsRequired = false,
                            Description = "Nominal display size."
                        }
                    ]
                }
            });
        var controller = new CategoriesController(service);

        var result = await controller.GetCategorySchema("tv");

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var payload = ok!.Value as CategorySchemaDto;
        Assert.That(payload, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(payload!.CategoryKey, Is.EqualTo("tv"));
            Assert.That(payload.Attributes, Has.Count.EqualTo(1));
            Assert.That(payload.Attributes[0].Key, Is.EqualTo("screen_size_inch"));
            Assert.That(payload.Attributes[0].Unit, Is.EqualTo("inch"));
        });
    }

    [Test]
    public async Task GetCategorySchema_ReturnsNotFoundForUnknownCategory()
    {
        var controller = new CategoriesController(new FakeCategoryManagementService());

        var result = await controller.GetCategorySchema("unknown-category");

        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task GetEnabledCategories_ReturnsOnlyEnabledCrawlableCategories()
    {
        var service = new FakeCategoryManagementService(
        [
            CreateCategory("tv", "TVs", "display", "Display", CrawlSupportStatus.Supported, true),
            CreateCategory("monitor", "Monitors", "display", "Display", CrawlSupportStatus.Experimental, true),
            CreateCategory("laptop", "Laptops", "computing", "Computing", CrawlSupportStatus.Planned, true),
            CreateCategory("refrigerator", "Refrigerators", "kitchen_appliances", "Kitchen Appliances", CrawlSupportStatus.Disabled, true)
        ]);
        var controller = new CategoriesController(service);

        var result = await controller.GetEnabledCategories();

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var payload = ok!.Value as CategoryMetadataDto[];
        Assert.That(payload, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(payload!, Has.Length.EqualTo(2));
            Assert.That(payload.Select(category => category.CategoryKey), Is.EqualTo(new[] { "monitor", "tv" }));
        });
    }

    [Test]
    public async Task GetEnabledCategories_ReturnsEmptyArrayWhenNoEnabledCrawlableCategoriesExist()
    {
        var service = new FakeCategoryManagementService(
        [
            CreateCategory("laptop", "Laptops", "computing", "Computing", CrawlSupportStatus.Planned, true),
            CreateCategory("refrigerator", "Refrigerators", "kitchen_appliances", "Kitchen Appliances", CrawlSupportStatus.Disabled, false)
        ]);
        var controller = new CategoriesController(service);

        var result = await controller.GetEnabledCategories();

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var payload = ok!.Value as CategoryMetadataDto[];
        Assert.That(payload, Is.Empty);
    }

    [Test]
    public async Task UpsertCategory_AllowsApiToAmendMetadata()
    {
        var service = new FakeCategoryManagementService();
        var controller = new CategoriesController(service);

        var result = await controller.UpsertCategory("monitor", new UpsertCategoryMetadataRequest
        {
            DisplayName = "Monitors",
            FamilyKey = "display",
            FamilyDisplayName = "Display",
            IconKey = "monitor",
            CrawlSupportStatus = "Experimental",
            SchemaCompletenessScore = 0.5m,
            IsEnabled = true
        });

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var payload = ok!.Value as CategoryMetadataDto;
        Assert.That(payload, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(payload!.CategoryKey, Is.EqualTo("monitor"));
            Assert.That(payload.CrawlSupportStatus, Is.EqualTo("Experimental"));
            Assert.That(service.StoredCategory!.IsEnabled, Is.True);
        });
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
            SchemaCompletenessScore = 0.50m,
            IsEnabled = isEnabled
        };
    }

    private sealed class FakeCategoryManagementService(
        IReadOnlyList<CategoryMetadata>? categories = null,
        IReadOnlyDictionary<string, CategorySchema>? schemas = null) : ICategoryManagementService
    {
        private readonly List<CategoryMetadata> items = (categories ?? []).ToList();
        private readonly Dictionary<string, CategorySchema> schemasByKey = new(schemas ?? new Dictionary<string, CategorySchema>(), StringComparer.OrdinalIgnoreCase);

        public CategoryMetadata? StoredCategory { get; private set; }

        public Task<IReadOnlyList<CategoryMetadata>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<CategoryMetadata>>(items.ToArray());
        }

        public Task<IReadOnlyList<CategoryMetadata>> ListEnabledAsync(CancellationToken cancellationToken = default)
        {
            var result = items
                .Where(category => category.IsEnabled && category.CrawlSupportStatus is CrawlSupportStatus.Supported or CrawlSupportStatus.Experimental)
                .OrderBy(category => category.FamilyDisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(category => category.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return Task.FromResult<IReadOnlyList<CategoryMetadata>>(result);
        }

        public Task<IReadOnlyList<CategoryFamily>> ListFamiliesAsync(CancellationToken cancellationToken = default)
        {
            var result = items
                .GroupBy(category => new { category.FamilyKey, category.FamilyDisplayName })
                .Select(group => new CategoryFamily
                {
                    FamilyKey = group.Key.FamilyKey,
                    FamilyDisplayName = group.Key.FamilyDisplayName,
                    Categories = group.OrderBy(category => category.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray()
                })
                .OrderBy(family => family.FamilyDisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return Task.FromResult<IReadOnlyList<CategoryFamily>>(result);
        }

        public Task<CategoryMetadata?> GetAsync(string categoryKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(items.FirstOrDefault(category => string.Equals(category.CategoryKey, categoryKey, StringComparison.OrdinalIgnoreCase)));
        }

        public async Task<CategoryDetail?> GetDetailAsync(string categoryKey, CancellationToken cancellationToken = default)
        {
            var category = await GetAsync(categoryKey, cancellationToken);
            if (category is null || !schemasByKey.TryGetValue(categoryKey, out var schema))
            {
                return null;
            }

            return new CategoryDetail
            {
                Metadata = category,
                Schema = schema
            };
        }

        public Task<CategorySchema?> GetSchemaAsync(string categoryKey, CancellationToken cancellationToken = default)
        {
            schemasByKey.TryGetValue(categoryKey, out var schema);
            return Task.FromResult(schema);
        }

        public Task<CategoryMetadata> UpsertAsync(CategoryMetadata categoryMetadata, CancellationToken cancellationToken = default)
        {
            StoredCategory = categoryMetadata;
            items.RemoveAll(category => string.Equals(category.CategoryKey, categoryMetadata.CategoryKey, StringComparison.OrdinalIgnoreCase));
            items.Add(categoryMetadata);
            return Task.FromResult(categoryMetadata);
        }
    }
}