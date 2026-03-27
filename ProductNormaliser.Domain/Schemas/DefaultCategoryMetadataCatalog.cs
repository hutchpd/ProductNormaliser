using ProductNormaliser.Core.Models;

namespace ProductNormaliser.Core.Schemas;

public static class DefaultCategoryMetadataCatalog
{
    private static readonly IReadOnlyList<CategoryMetadata> Categories =
    [
        Create("tv", "TVs", "display", "Display", "tv", CrawlSupportStatus.Supported, 1.00m, true),
        Create("monitor", "Monitors", "display", "Display", "monitor", CrawlSupportStatus.Supported, 0.89m, true),
        Create("laptop", "Laptops", "computing", "Computing", "laptop", CrawlSupportStatus.Supported, 0.87m, true),
        Create("tablet", "Tablets", "mobile", "Mobile", "tablet", CrawlSupportStatus.Supported, 0.90m, true),
        Create("smartphone", "Smartphones", "mobile", "Mobile", "smartphone", CrawlSupportStatus.Supported, 0.91m, true),
        Create("headphones", "Headphones", "audio", "Audio", "headphones", CrawlSupportStatus.Supported, 0.89m, true),
        Create("speakers", "Speakers", "audio", "Audio", "speaker", CrawlSupportStatus.Supported, 0.88m, true),
        Create("refrigerator", "Refrigerators", "kitchen_appliances", "Kitchen Appliances", "refrigerator", CrawlSupportStatus.Planned, 0.10m, false),
        Create("washing_machine", "Washing Machines", "home_appliances", "Home Appliances", "washing-machine", CrawlSupportStatus.Planned, 0.10m, false),
        Create("dishwasher", "Dishwashers", "kitchen_appliances", "Kitchen Appliances", "dishwasher", CrawlSupportStatus.Planned, 0.10m, false),
        Create("vacuum_cleaner", "Vacuum Cleaners", "home_appliances", "Home Appliances", "vacuum", CrawlSupportStatus.Planned, 0.10m, false),
        Create("microwave", "Microwaves", "kitchen_appliances", "Kitchen Appliances", "microwave", CrawlSupportStatus.Planned, 0.10m, false)
    ];

    public static IReadOnlyList<CategoryMetadata> GetAll()
    {
        return Categories.Select(Clone).ToArray();
    }

    public static CategoryMetadata? GetByKey(string categoryKey)
    {
        if (string.IsNullOrWhiteSpace(categoryKey))
        {
            return null;
        }

        return Categories
            .FirstOrDefault(category => string.Equals(category.CategoryKey, categoryKey, StringComparison.OrdinalIgnoreCase)) is { } category
            ? Clone(category)
            : null;
    }

    private static CategoryMetadata Create(
        string categoryKey,
        string displayName,
        string familyKey,
        string familyDisplayName,
        string iconKey,
        CrawlSupportStatus crawlSupportStatus,
        decimal schemaCompletenessScore,
        bool isEnabled)
    {
        return new CategoryMetadata
        {
            CategoryKey = categoryKey,
            DisplayName = displayName,
            FamilyKey = familyKey,
            FamilyDisplayName = familyDisplayName,
            IconKey = iconKey,
            CrawlSupportStatus = crawlSupportStatus,
            SchemaCompletenessScore = schemaCompletenessScore,
            IsEnabled = isEnabled
        };
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