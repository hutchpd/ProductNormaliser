using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Domain.Tests;

public sealed class CategorySchemaRegistryTests
{
    [Test]
    public void Registry_ResolvesExpectedProviders_ForTvMonitorLaptopAndRefrigerator()
    {
        var registry = new CategorySchemaRegistry(
        [
            new TvCategorySchemaProvider(),
            new MonitorCategorySchemaProvider(),
            new LaptopCategorySchemaProvider(),
            new RefrigeratorCategorySchemaProvider()
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(registry.GetProvider("tv"), Is.TypeOf<TvCategorySchemaProvider>());
            Assert.That(registry.GetProvider("monitor"), Is.TypeOf<MonitorCategorySchemaProvider>());
            Assert.That(registry.GetProvider("laptop"), Is.TypeOf<LaptopCategorySchemaProvider>());
            Assert.That(registry.GetProvider("refrigerator"), Is.TypeOf<RefrigeratorCategorySchemaProvider>());
        });
    }

    [Test]
    public void Registry_HandlesUnknownCategoriesExplicitlyAndSafely()
    {
        var registry = new CategorySchemaRegistry([new TvCategorySchemaProvider()]);

        var resolved = registry.TryGetSchema("smartwatch", out _);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.False);
            Assert.That(registry.GetProvider("smartwatch"), Is.Null);
            Assert.That(registry.GetSchema("smartwatch"), Is.Null);
        });
    }
}