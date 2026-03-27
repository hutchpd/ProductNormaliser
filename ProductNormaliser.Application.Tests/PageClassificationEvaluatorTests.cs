using ProductNormaliser.Application.AI;
using ProductNormaliser.Infrastructure.AI;
using ProductNormaliser.Infrastructure.StructuredData;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.AIClassification)]
public sealed class PageClassificationEvaluatorTests
{
    [Test]
    public async Task RunAsync_ProducesStructuredSummaryWithFalsePositiveAndFalseNegativeTracking()
    {
        var evaluator = new PageClassificationEvaluator(
            new FixtureAwarePageClassificationService(),
            new SchemaOrgJsonLdExtractor(),
            new TestLogger<PageClassificationEvaluator>());

        var results = await evaluator.RunAsync(CreateDataset(), CancellationToken.None);
        var summary = PageClassificationEvaluator.Summarize(results);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Total, Is.EqualTo(10));
            Assert.That(summary.LlmAccuracy, Is.GreaterThanOrEqualTo(80d));
            Assert.That(summary.HeuristicAccuracy, Is.GreaterThanOrEqualTo(50d));
            Assert.That(summary.CombinedAccuracy, Is.GreaterThanOrEqualTo(summary.LlmAccuracy));
            Assert.That(summary.FalsePositives, Is.GreaterThanOrEqualTo(0));
            Assert.That(summary.FalseNegatives, Is.GreaterThanOrEqualTo(0));
            Assert.That(results.Any(item => item.FalsePositive || item.FalseNegative), Is.True);
        });
    }

    [Test]
    public async Task ClassificationConfidence_IsConsistentAcrossDataset()
    {
        var evaluator = new PageClassificationEvaluator(
            new FixtureAwarePageClassificationService(),
            new SchemaOrgJsonLdExtractor());

        var results = await evaluator.RunAsync(CreateDataset(), CancellationToken.None);
        var highConfidence = results.Where(result => result.Confidence >= 0.8d).ToArray();
        var lowConfidence = results.Where(result => result.Confidence <= 0.2d).ToArray();

        var highConfidenceAccuracy = highConfidence.Count(result => result.FinalDecision == result.ExpectedIsProduct) / (double)highConfidence.Length;
        var lowConfidencePositives = lowConfidence.Count(result => result.FinalDecision);

        Assert.Multiple(() =>
        {
            Assert.That(highConfidence.Length, Is.GreaterThan(0));
            Assert.That(lowConfidence.Length, Is.GreaterThan(0));
            Assert.That(highConfidenceAccuracy, Is.GreaterThanOrEqualTo(0.75d));
            Assert.That(lowConfidencePositives, Is.EqualTo(0));
        });
    }

    private static IReadOnlyList<PageClassificationEvaluationCase> CreateDataset()
    {
        return
        [
            CreateCase("single-product", "single-product.html", true, "https://shop.example/product/oled-123"),
            CreateCase("spec-table-product", "spec-table-product.html", true, "https://shop.example/product/spec-tv-1"),
            CreateCase("js-heavy-product", "js-heavy-product.html", true, "https://shop.example/product/js-heavy-tv"),
            CreateCase("minimal-spec-product", "minimal-spec-product.html", true, "https://shop.example/product/minimal-spec-tv"),
            CreateCase("manufacturer-weak-structure", "manufacturer-weak-structure.html", true, "https://brand.example/product/zx900"),
            CreateCase("support-article", "support-article.html", false, "https://shop.example/support/order-delay"),
            CreateCase("category-listing", "category-listing-page.html", false, "https://shop.example/category/televisions"),
            CreateCase("comparison", "comparison-page.html", false, "https://shop.example/blog/oled-vs-qled"),
            CreateCase("review", "review-article.html", false, "https://shop.example/reviews/top-oled-tvs"),
            CreateCase("best-tvs-2024", "best-tvs-2024.html", false, "https://shop.example/blog/best-tvs-2024")
        ];
    }

    private static PageClassificationEvaluationCase CreateCase(string name, string fileName, bool expectedIsProduct, string url)
    {
        return new PageClassificationEvaluationCase
        {
            Name = name,
            Category = "tv",
            Url = url,
            Content = EmbeddedHtmlFixtureLoader.Load(fileName),
            ExpectedIsProduct = expectedIsProduct
        };
    }

    private sealed class FixtureAwarePageClassificationService : IPageClassificationService
    {
        public Task<PageClassificationResult> ClassifyAsync(string content, string category, CancellationToken cancellationToken = default)
        {
            if (content.Contains("application/ld+json", StringComparison.OrdinalIgnoreCase)
                || content.Contains("Add to basket", StringComparison.OrdinalIgnoreCase)
                || content.Contains("Buy now", StringComparison.OrdinalIgnoreCase)
                || content.Contains("Specifications", StringComparison.OrdinalIgnoreCase) && content.Contains("<table", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new PageClassificationResult
                {
                    IsProductPage = true,
                    HasSpecifications = true,
                    DetectedCategory = category,
                    Confidence = 0.85d,
                    Reason = "Synthetic LLM accepted the page as product-like."
                });
            }

            if (content.Contains("review", StringComparison.OrdinalIgnoreCase)
                || content.Contains("comparison", StringComparison.OrdinalIgnoreCase)
                || content.Contains("best TVs 2024", StringComparison.OrdinalIgnoreCase)
                || content.Contains("support article", StringComparison.OrdinalIgnoreCase)
                || content.Contains("product-grid", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new PageClassificationResult
                {
                    IsProductPage = false,
                    HasSpecifications = false,
                    Confidence = 0.1d,
                    Reason = "Synthetic LLM rejected the page as non-product content."
                });
            }

            return Task.FromResult(new PageClassificationResult
            {
                IsProductPage = false,
                HasSpecifications = false,
                Confidence = 0.2d,
                Reason = "LLM low confidence"
            });
        }
    }
}