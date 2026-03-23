using ProductNormaliser.Core.Normalisation;

namespace ProductNormaliser.Core.Schemas;

public static class DefaultCategoryRegistries
{
    public static IReadOnlyList<ICategorySchemaProvider> CreateSchemaProviders()
    {
        return
        [
            new TvCategorySchemaProvider(),
            new MonitorCategorySchemaProvider(),
            new LaptopCategorySchemaProvider(),
            new TabletCategorySchemaProvider(),
            new SmartphoneCategorySchemaProvider(),
            new HeadphonesCategorySchemaProvider(),
            new SpeakersCategorySchemaProvider(),
            new RefrigeratorCategorySchemaProvider()
        ];
    }

    public static CategorySchemaRegistry CreateSchemaRegistry()
    {
        return new CategorySchemaRegistry(CreateSchemaProviders());
    }

    public static IReadOnlyList<ICategoryAttributeNormaliser> CreateAttributeNormalisers()
    {
        return
        [
            new TvAttributeNormaliser(),
            new MonitorAttributeNormaliser(),
            new LaptopAttributeNormaliser(),
            new TabletAttributeNormaliser(),
            new SmartphoneAttributeNormaliser(),
            new HeadphonesAttributeNormaliser(),
            new SpeakersAttributeNormaliser(),
            new RefrigeratorAttributeNormaliser()
        ];
    }

    public static CategoryAttributeNormaliserRegistry CreateAttributeNormaliserRegistry()
    {
        return new CategoryAttributeNormaliserRegistry(CreateAttributeNormalisers());
    }
}