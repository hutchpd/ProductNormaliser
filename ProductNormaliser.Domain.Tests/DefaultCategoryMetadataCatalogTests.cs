using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Domain.Tests;

public sealed class DefaultCategoryMetadataCatalogTests
{
    [Test]
    public void GetAll_ReturnsExpectedInitialCategoriesAndFamilies()
    {
        var categories = DefaultCategoryMetadataCatalog.GetAll();

        Assert.Multiple(() =>
        {
            Assert.That(categories, Has.Count.EqualTo(12));
            Assert.That(categories.Select(category => category.CategoryKey), Is.EquivalentTo(new[]
            {
                "tv",
                "monitor",
                "laptop",
                "tablet",
                "smartphone",
                "headphones",
                "speakers",
                "refrigerator",
                "washing_machine",
                "dishwasher",
                "vacuum_cleaner",
                "microwave"
            }));
            Assert.That(categories.Select(category => category.FamilyKey).Distinct(StringComparer.OrdinalIgnoreCase), Is.EquivalentTo(new[]
            {
                "display",
                "computing",
                "mobile",
                "audio",
                "kitchen_appliances",
                "home_appliances"
            }));
        });
    }

    [Test]
    public void GetByKey_ReturnsTvAsSupportedEnabledCategory()
    {
        var category = DefaultCategoryMetadataCatalog.GetByKey("tv");

        Assert.That(category, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(category!.DisplayName, Is.EqualTo("TVs"));
            Assert.That(category.CrawlSupportStatus, Is.EqualTo(CrawlSupportStatus.Supported));
            Assert.That(category.SchemaCompletenessScore, Is.EqualTo(1.00m));
            Assert.That(category.IsEnabled, Is.True);
        });
    }

    [Test]
    public void GetAll_EnablesSupportedAndExperimentalActiveCategories()
    {
        var enabledCategories = DefaultCategoryMetadataCatalog.GetAll()
            .Where(category => category.IsEnabled)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(enabledCategories.Select(category => category.CategoryKey), Is.EqualTo(new[] { "tv", "monitor", "laptop", "tablet", "smartphone", "headphones", "speakers" }));
            Assert.That(enabledCategories.Count(category => category.CrawlSupportStatus == CrawlSupportStatus.Supported), Is.EqualTo(3));
            Assert.That(enabledCategories.Count(category => category.CrawlSupportStatus == CrawlSupportStatus.Experimental), Is.EqualTo(4));
        });
    }
}