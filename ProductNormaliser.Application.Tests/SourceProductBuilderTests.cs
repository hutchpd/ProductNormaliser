using ProductNormaliser.Infrastructure.StructuredData;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.Extraction)]
public sealed class SourceProductBuilderTests
{
    [Test]
    public void Build_CreatesSourceProductWithRawAttributesAndTraceability()
    {
        var extractor = new SchemaOrgJsonLdExtractor();
        var builder = new SourceProductBuilder();
        var html = EmbeddedHtmlFixtureLoader.Load("single-product.html");
        var extractedProduct = extractor.ExtractProducts(html, "https://example.com/products/samsung").Single();
        var fetchedUtc = new DateTime(2026, 03, 20, 12, 00, 00, DateTimeKind.Utc);

        var sourceProduct = builder.Build("example-retailer", "tv", extractedProduct, fetchedUtc);

        Assert.Multiple(() =>
        {
            Assert.That(sourceProduct.Id, Does.StartWith("example-retailer:"));
            Assert.That(sourceProduct.SourceName, Is.EqualTo("example-retailer"));
            Assert.That(sourceProduct.CategoryKey, Is.EqualTo("tv"));
            Assert.That(sourceProduct.Brand, Is.EqualTo("Samsung"));
            Assert.That(sourceProduct.ModelNumber, Is.EqualTo("QE55S90D"));
            Assert.That(sourceProduct.Title, Is.EqualTo("Samsung QE55S90D OLED TV"));
            Assert.That(sourceProduct.RawAttributes["Screen Size"].Value, Is.EqualTo("55 in"));
            Assert.That(sourceProduct.RawAttributes["Screen Size"].ValueType, Is.EqualTo("string"));
            Assert.That(sourceProduct.RawSchemaJson, Does.Contain("QE55S90D"));
            Assert.That(sourceProduct.Offers, Has.Count.EqualTo(1));
            Assert.That(sourceProduct.Offers[0].Price, Is.EqualTo(1299.99m));
            Assert.That(sourceProduct.Offers[0].ObservedUtc, Is.EqualTo(fetchedUtc));
        });
    }
}