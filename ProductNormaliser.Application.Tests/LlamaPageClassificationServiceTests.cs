using ProductNormaliser.Application.AI;
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
            Assert.That(result.LlmStatus, Is.EqualTo(LlmStatusCodes.Active));
            Assert.That(result.Reason, Does.Contain("accepted"));
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
            Assert.That(result.Confidence, Is.EqualTo(0d));
            Assert.That(result.LlmStatus, Is.EqualTo(LlmStatusCodes.Active));
            Assert.That(result.Reason, Is.EqualTo("LLM low confidence"));
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

    [Test]
    public async Task ClassifyAsync_TimesOutGracefully()
    {
        var service = new LlamaPageClassificationService(
            new LlmOptions { ModelPath = "models/test.gguf", TimeoutMs = 25 },
            async (_, cancellationToken) =>
            {
                await Task.Delay(250, cancellationToken);
                return "YES";
            });

        var result = await service.ClassifyAsync("content", "tv", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsProductPage, Is.False);
            Assert.That(result.Confidence, Is.EqualTo(0d));
            Assert.That(result.LlmStatus, Is.EqualTo(LlmStatusCodes.Active));
            Assert.That(result.Reason, Is.EqualTo("LLM timeout"));
        });
    }

    [Test]
    public async Task ClassifyAsync_ReturnsUnconfiguredWhenConfiguredModelIsMissing()
    {
        var service = new LlamaPageClassificationService(new LlmOptions
        {
            Enabled = true,
            ModelPath = "models/missing.gguf"
        });

        var result = await service.ClassifyAsync("content", "tv", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsProductPage, Is.False);
            Assert.That(result.Confidence, Is.EqualTo(0d));
            Assert.That(result.LlmStatus, Is.EqualTo(LlmStatusCodes.Unconfigured));
            Assert.That(result.Reason, Is.EqualTo("LLM unconfigured"));
        });
    }

    [Test]
    public async Task ClassifyAsync_UsesSuppliedContentRootToResolveRelativeModelPath()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"llm-root-{Guid.NewGuid():N}");
        var modelsDir = Path.Combine(tempRoot, "models");
        Directory.CreateDirectory(modelsDir);
        await File.WriteAllTextAsync(Path.Combine(modelsDir, "tinyllama.gguf"), "placeholder", CancellationToken.None);

        try
        {
            var service = new LlamaPageClassificationService(
                new LlmOptions
                {
                    Enabled = true,
                    ModelPath = "models/tinyllama.gguf"
                },
                (_, _) => Task.FromResult("YES"),
                tempRoot);

            var result = await service.ClassifyAsync("content", "tv", CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsProductPage, Is.True);
                Assert.That(result.LlmStatus, Is.EqualTo(LlmStatusCodes.Active));
                Assert.That(result.Reason, Does.Contain("accepted"));
            });
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Test]
    public async Task ClassifyAsync_ReturnsRuntimeFailedWhenInferenceThrows()
    {
        var service = new LlamaPageClassificationService(
            new LlmOptions { ModelPath = "models/test.gguf" },
            (_, _) => throw new InvalidOperationException("boom"));

        var result = await service.ClassifyAsync("content", "tv", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsProductPage, Is.False);
            Assert.That(result.Confidence, Is.EqualTo(0d));
            Assert.That(result.LlmStatus, Is.EqualTo(LlmStatusCodes.RuntimeFailed));
            Assert.That(result.Reason, Is.EqualTo("LLM runtime failed"));
        });
    }

    [Test]
    public async Task ClassifyAsync_ReturnsLoadFailedWhenConfiguredModelCannotBeLoaded()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"llm-invalid-{Guid.NewGuid():N}.gguf");
        await File.WriteAllTextAsync(tempPath, "not-a-valid-gguf", CancellationToken.None);

        try
        {
            var service = new LlamaPageClassificationService(new LlmOptions
            {
                Enabled = true,
                ModelPath = tempPath
            });

            var result = await service.ClassifyAsync("content", "tv", CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsProductPage, Is.False);
                Assert.That(result.Confidence, Is.EqualTo(0d));
                Assert.That(result.LlmStatus, Is.EqualTo(LlmStatusCodes.LoadFailed));
                Assert.That(result.Reason, Is.EqualTo("LLM load failed"));
            });
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}