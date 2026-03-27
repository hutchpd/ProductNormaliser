using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Intelligence;
using ProductNormaliser.Infrastructure.Mongo;
using ProductNormaliser.Infrastructure.Mongo.Repositories;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.Intelligence)]
public sealed class SourceDisagreementServiceTests
{
    [SetUp]
    public async Task SetUpAsync()
    {
        var context = MongoIntegrationTestFixture.Context;
        await context.Database.DropCollectionIfExistsAsync(MongoCollectionNames.SourceAttributeDisagreements);
        await context.EnsureIndexesAsync();
    }

    [Test]
    public async Task RefreshForProduct_TracksWhoDisagreesAndWhoWins()
    {
        var context = MongoIntegrationTestFixture.Context;
        var service = new SourceDisagreementService(new SourceAttributeDisagreementRepository(context));

        service.RefreshForProduct(new CanonicalProduct
        {
            Id = "canonical-1",
            CategoryKey = "tv",
            UpdatedUtc = DateTime.UtcNow,
            Attributes = new Dictionary<string, CanonicalAttributeValue>
            {
                ["refresh_rate_hz"] = new()
                {
                    AttributeKey = "refresh_rate_hz",
                    Value = 120,
                    ValueType = "integer",
                    WinningSourceName = "manufacturer",
                    Evidence =
                    [
                        new AttributeEvidence { SourceName = "manufacturer", SourceUrl = "https://m/1", SourceProductId = "m1", SourceAttributeKey = "Refresh Rate", RawValue = "120", Confidence = 0.99m, ObservedUtc = DateTime.UtcNow },
                        new AttributeEvidence { SourceName = "retailer", SourceUrl = "https://r/1", SourceProductId = "r1", SourceAttributeKey = "Refresh Rate", RawValue = "900", Confidence = 0.50m, ObservedUtc = DateTime.UtcNow }
                    ]
                }
            }
        });

        var disagreements = await new SourceAttributeDisagreementRepository(context).ListAsync("tv");
        var retailer = disagreements.Single(item => item.SourceName == "retailer");
        var manufacturer = disagreements.Single(item => item.SourceName == "manufacturer");

        Assert.Multiple(() =>
        {
            Assert.That(retailer.DisagreementRate, Is.EqualTo(1.00m));
            Assert.That(retailer.WinRate, Is.EqualTo(0.00m));
            Assert.That(manufacturer.DisagreementRate, Is.EqualTo(0.00m));
            Assert.That(manufacturer.WinRate, Is.EqualTo(1.00m));
            Assert.That(service.GetSourceAttributeAdjustment("retailer", "tv", "refresh_rate_hz"), Is.LessThan(1.00m));
        });
    }
}