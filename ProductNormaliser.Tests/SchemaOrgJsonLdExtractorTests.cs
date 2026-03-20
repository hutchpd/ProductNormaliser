using ProductNormaliser.Infrastructure.StructuredData;

namespace ProductNormaliser.Tests;

public sealed class SchemaOrgJsonLdExtractorTests
{
    private const string SourceUrl = "https://example.com/products/test-tv";

    [Test]
    public void ExtractProducts_ParsesSingleProductJsonLd()
    {
        var html = EmbeddedHtmlFixtureLoader.Load("single-product.html");
        var sut = new SchemaOrgJsonLdExtractor();

        var products = sut.ExtractProducts(html, SourceUrl);
        var product = products.Single();

        Assert.Multiple(() =>
        {
            Assert.That(product.SourceUrl, Is.EqualTo(SourceUrl));
            Assert.That(product.Name, Is.EqualTo("Samsung QE55S90D OLED TV"));
            Assert.That(product.Brand, Is.EqualTo("Samsung"));
            Assert.That(product.Gtin, Is.EqualTo("8806095563140"));
            Assert.That(product.ModelNumber, Is.EqualTo("QE55S90D"));
            Assert.That(product.RawJson, Does.Contain("\"@type\": \"Product\""));
        });
    }

    [Test]
    public void ExtractProducts_ExtractsAdditionalProperties()
    {
        var html = EmbeddedHtmlFixtureLoader.Load("single-product.html");
        var sut = new SchemaOrgJsonLdExtractor();

        var product = sut.ExtractProducts(html, SourceUrl).Single();

        Assert.Multiple(() =>
        {
            Assert.That(product.Attributes["Screen Size"], Is.EqualTo("55 in"));
            Assert.That(product.Attributes["HDMI Ports"], Is.EqualTo("4"));
            Assert.That(product.Attributes["sku"], Is.EqualTo("QE55S90D"));
        });
    }

    [Test]
    public void ExtractProducts_ExtractsOffersFromGraphReferences()
    {
        var html = EmbeddedHtmlFixtureLoader.Load("graph-product.html");
        var sut = new SchemaOrgJsonLdExtractor();

        var product = sut.ExtractProducts(html, SourceUrl).Single();

        Assert.Multiple(() =>
        {
            Assert.That(product.Brand, Is.EqualTo("LG"));
            Assert.That(product.ModelNumber, Is.EqualTo("OLED55C4"));
            Assert.That(product.Attributes["Refresh Rate"], Is.EqualTo("120 Hz"));
            Assert.That(product.Offers, Has.Count.EqualTo(1));
            Assert.That(product.Offers[0].Price, Is.EqualTo(1499.00m));
            Assert.That(product.Offers[0].Currency, Is.EqualTo("EUR"));
            Assert.That(product.Offers[0].Availability, Is.EqualTo("https://schema.org/PreOrder"));
        });
    }

    [Test]
    public void ExtractProducts_HandlesMissingOptionalFields()
    {
        var html = EmbeddedHtmlFixtureLoader.Load("missing-fields.html");
        var sut = new SchemaOrgJsonLdExtractor();

        var product = sut.ExtractProducts(html, SourceUrl).Single();

        Assert.Multiple(() =>
        {
            Assert.That(product.Name, Is.EqualTo("Mystery Television"));
            Assert.That(product.Brand, Is.Null);
            Assert.That(product.Gtin, Is.Null);
            Assert.That(product.ModelNumber, Is.Null);
            Assert.That(product.Attributes, Is.Empty);
            Assert.That(product.Offers, Is.Empty);
        });
    }

    [Test]
    public void ExtractProducts_ReturnsMultipleProductsFromOnePage()
    {
        var html = EmbeddedHtmlFixtureLoader.Load("multiple-products.html");
        var sut = new SchemaOrgJsonLdExtractor();

        var products = sut.ExtractProducts(html, SourceUrl);

        Assert.Multiple(() =>
        {
            Assert.That(products, Has.Count.EqualTo(2));
            Assert.That(products.Select(product => product.Name), Is.EquivalentTo(new[] { "Sony Bravia 55", "Panasonic MX800" }));
        });
    }

    [Test]
    public void ExtractProducts_SkipsMalformedJsonLdWithoutCrashing()
    {
        var html = EmbeddedHtmlFixtureLoader.Load("malformed-and-valid.html");
        var sut = new SchemaOrgJsonLdExtractor();

        Assert.DoesNotThrow(() => sut.ExtractProducts(html, SourceUrl));

        var products = sut.ExtractProducts(html, SourceUrl).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(products, Has.Length.EqualTo(1));
            Assert.That(products[0].Name, Is.EqualTo("Recovered Product"));
            Assert.That(products[0].Brand, Is.EqualTo("Hisense"));
        });
    }
}