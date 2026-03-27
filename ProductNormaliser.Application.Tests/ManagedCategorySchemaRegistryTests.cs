using ProductNormaliser.Application.Categories;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;
using ProductNormaliser.Infrastructure.Schemas;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.CategorySchema)]
public sealed class ManagedCategorySchemaRegistryTests
{
    [Test]
    public void GetSchema_UsesManagedSchemaAttributesWhenPresent()
    {
        var baseSchema = new TvCategorySchemaProvider().GetSchema();
        var managedAttributes = baseSchema.Attributes
            .Select(attribute => new CanonicalAttributeDefinition
            {
                Key = attribute.Key,
                DisplayName = attribute.DisplayName,
                ValueType = attribute.ValueType,
                Unit = attribute.Unit,
                IsRequired = attribute.Key is "brand" or "model_number" or "screen_size_inch",
                ConflictSensitivity = attribute.ConflictSensitivity,
                Description = attribute.Description
            })
            .Append(new CanonicalAttributeDefinition
            {
                Key = "display_port_count",
                DisplayName = "Display Port Count",
                ValueType = "integer",
                IsRequired = true,
                ConflictSensitivity = ConflictSensitivity.Medium,
                Description = "Number of DisplayPort inputs."
            })
            .ToList();

        var store = new InMemoryCategoryMetadataStore(new CategoryMetadata
        {
            CategoryKey = "tv",
            DisplayName = "TVs",
            FamilyKey = "display",
            FamilyDisplayName = "Display",
            IconKey = "tv",
            CrawlSupportStatus = CrawlSupportStatus.Supported,
            SchemaCompletenessScore = 1.0m,
            IsEnabled = true,
            ManagedSchemaAttributes = managedAttributes
        });
        var registry = new ManagedCategorySchemaRegistry(new CategorySchemaRegistry([new TvCategorySchemaProvider()]), store);

        var schema = registry.GetSchema("tv");

        Assert.That(schema, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(schema!.Attributes.Any(attribute => attribute.Key == "display_port_count" && attribute.IsRequired), Is.True);
            Assert.That(schema.Attributes.Single(attribute => attribute.Key == "screen_size_inch").IsRequired, Is.True);
            Assert.That(schema.Attributes.Count, Is.EqualTo(managedAttributes.Count));
        });
    }

    private sealed class InMemoryCategoryMetadataStore(params CategoryMetadata[] categories) : ICategoryMetadataStore
    {
        private readonly Dictionary<string, CategoryMetadata> items = categories.ToDictionary(category => category.CategoryKey, Clone, StringComparer.OrdinalIgnoreCase);

        public CategoryMetadata? Get(string categoryKey)
        {
            return items.TryGetValue(categoryKey, out var category) ? Clone(category) : null;
        }

        public Task<CategoryMetadata?> GetAsync(string categoryKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Get(categoryKey));
        }

        public IReadOnlyList<CategoryMetadata> List()
        {
            return items.Values.Select(Clone).ToArray();
        }

        public Task<IReadOnlyList<CategoryMetadata>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(List());
        }

        public Task UpsertAsync(CategoryMetadata categoryMetadata, CancellationToken cancellationToken = default)
        {
            items[categoryMetadata.CategoryKey] = Clone(categoryMetadata);
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
                IsEnabled = category.IsEnabled,
                ManagedSchemaAttributes = category.ManagedSchemaAttributes
                    .Select(attribute => new CanonicalAttributeDefinition
                    {
                        Key = attribute.Key,
                        DisplayName = attribute.DisplayName,
                        ValueType = attribute.ValueType,
                        Unit = attribute.Unit,
                        IsRequired = attribute.IsRequired,
                        ConflictSensitivity = attribute.ConflictSensitivity,
                        Description = attribute.Description
                    })
                    .ToList()
            };
        }
    }
}