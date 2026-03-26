using ProductNormaliser.Infrastructure.AI;

namespace ProductNormaliser.Tests;

public sealed class LlamaPageClassificationServiceTests
{
    [Test]
    public async Task ClassifyAsync_ReturnsYesForKnownProductPageFixture()
    {
        var html = EmbeddedHtmlFixtureLoader.Load("single-product.html");
        var service = new LlamaPageClassificationService(
            new LlmOptions { ModelPath = "models/test.gguf" },
            (prompt, _) => Task.FromResult(prompt.Contains("Product", StringComparison.OrdinalIgnoreCase) ? "YES" : "NO"));

        var result = await service.ClassifyAsync(html, "tv", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsProductPage, Is.True);
            Assert.That(result.HasSpecifications, Is.True);
            Assert.That(result.DetectedCategory, Is.EqualTo("tv"));
            Assert.That(result.Confidence, Is.EqualTo(0.8d));
        });
    }

    [Test]
    public async Task ClassifyAsync_ReturnsNoForSupportFixture()
    {
        var html = EmbeddedHtmlFixtureLoader.Load("support-article.html");
        var service = new LlamaPageClassificationService(
            new LlmOptions { ModelPath = "models/test.gguf" },
            (_, _) => Task.FromResult("NO"));

        var result = await service.ClassifyAsync(html, "tv", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsProductPage, Is.False);
            Assert.That(result.HasSpecifications, Is.False);
            Assert.That(result.DetectedCategory, Is.Null);
            Assert.That(result.Confidence, Is.EqualTo(0.2d));
        });
    }

    [Test]
    public async Task ClassifyAsync_TruncatesLongInputBeforeInference()
    {
        var leading = new string('A', 4000);
        var trailing = "TRAILING_CONTENT_THAT_SHOULD_NOT_BE_IN_PROMPT";
        var observedPrompt = string.Empty;
        var service = new LlamaPageClassificationService(
            new LlmOptions { ModelPath = "models/test.gguf" },
            (prompt, _) =>
            {
                observedPrompt = prompt;
                return Task.FromResult("YES");
            });

        await service.ClassifyAsync(leading + trailing, "tv", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(observedPrompt, Does.Contain(leading));
            Assert.That(observedPrompt, Does.Not.Contain(trailing));
            Assert.That(observedPrompt.Length, Is.LessThan((leading + trailing).Length + 200));
        });
    }
}