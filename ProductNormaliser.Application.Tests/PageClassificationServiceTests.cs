using ProductNormaliser.Application.AI;

namespace ProductNormaliser.Tests;

public sealed class PageClassificationServiceTests
{
    [Test]
    public async Task ClassifyAsync_ReturnsValidStructure()
    {
        var service = new NoOpPageClassificationService();

        var result = await service.ClassifyAsync("<html><body>product page</body></html>", "tv", CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsProductPage, Is.True);
            Assert.That(result.HasSpecifications, Is.True);
            Assert.That(result.Confidence, Is.InRange(0d, 1d));
        });
    }

    [Test]
    public void ClassifyAsync_DoesNotThrow()
    {
        var service = new NoOpPageClassificationService();

        Assert.DoesNotThrowAsync(async () =>
        {
            _ = await service.ClassifyAsync("content", "tv", CancellationToken.None);
        });
    }

    [Test]
    public async Task ClassifyAsync_HandlesEmptyContent()
    {
        var service = new NoOpPageClassificationService();

        var result = await service.ClassifyAsync(string.Empty, "tv", CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsProductPage, Is.True);
            Assert.That(result.HasSpecifications, Is.True);
            Assert.That(result.Confidence, Is.InRange(0d, 1d));
        });
    }
}
