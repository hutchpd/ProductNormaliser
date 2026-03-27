using ProductNormaliser.Application.AI;
using ProductNormaliser.Infrastructure.AI;
using ProductNormaliser.Infrastructure.StructuredData;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.AIClassification)]
public sealed class PageClassificationAccuracyTests
{
    [Test]
    public async Task GoldenDataset_MeasuresPrecisionAndRecall()
    {
        var evaluator = new PageClassificationEvaluator(
            new GoldenDatasetPageClassificationService(),
            new SchemaOrgJsonLdExtractor(),
            new TestLogger<PageClassificationEvaluator>());

        var results = await evaluator.RunAsync(LoadGoldenDataset(), CancellationToken.None);
        var summary = PageClassificationEvaluator.Summarize(results);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Total, Is.EqualTo(10));
            Assert.That(summary.CombinedPrecision, Is.GreaterThanOrEqualTo(80d));
            Assert.That(summary.CombinedRecall, Is.GreaterThanOrEqualTo(60d));
            Assert.That(summary.CombinedPrecision, Is.GreaterThanOrEqualTo(summary.HeuristicPrecision));
            Assert.That(summary.FalsePositives + summary.FalseNegatives, Is.LessThan(summary.Total));
        });
    }

    private static IReadOnlyList<PageClassificationEvaluationCase> LoadGoldenDataset()
    {
        var productCases = EmbeddedHtmlFixtureLoader.ListTestData("product_pages")
            .Select(fileName => CreateCase($"product-{Path.GetFileNameWithoutExtension(fileName)}", $"product_pages/{fileName}", true, $"https://golden.example/product/{Path.GetFileNameWithoutExtension(fileName)}"));
        var nonProductCases = EmbeddedHtmlFixtureLoader.ListTestData("non_product_pages")
            .Select(fileName => CreateCase($"non-product-{Path.GetFileNameWithoutExtension(fileName)}", $"non_product_pages/{fileName}", false, BuildNonProductUrl(fileName)));

        return productCases.Concat(nonProductCases).ToArray();
    }

    private static string BuildNonProductUrl(string fileName)
    {
        return Path.GetFileNameWithoutExtension(fileName) switch
        {
            var value when value.Contains("listing", StringComparison.OrdinalIgnoreCase) => $"https://golden.example/category/{value}",
            var value when value.Contains("comparison", StringComparison.OrdinalIgnoreCase) => $"https://golden.example/blog/{value}",
            var value when value.Contains("review", StringComparison.OrdinalIgnoreCase) => $"https://golden.example/reviews/{value}",
            var value when value.Contains("best", StringComparison.OrdinalIgnoreCase) => $"https://golden.example/blog/{value}",
            _ => $"https://golden.example/support/{Path.GetFileNameWithoutExtension(fileName)}"
        };
    }

    private static PageClassificationEvaluationCase CreateCase(string name, string relativePath, bool expectedIsProduct, string url)
    {
        return new PageClassificationEvaluationCase
        {
            Name = name,
            Category = "tv",
            Url = url,
            Content = EmbeddedHtmlFixtureLoader.LoadTestData(relativePath),
            ExpectedIsProduct = expectedIsProduct
        };
    }

    private sealed class GoldenDatasetPageClassificationService : IPageClassificationService
    {
        public Task<PageClassificationResult> ClassifyAsync(string content, string category, CancellationToken cancellationToken = default)
        {
            if (content.Contains("application/ld+json", StringComparison.OrdinalIgnoreCase)
                || content.Contains("Buy now", StringComparison.OrdinalIgnoreCase)
                || content.Contains("Add to basket", StringComparison.OrdinalIgnoreCase)
                || content.Contains("HDMI 2.1", StringComparison.OrdinalIgnoreCase)
                || content.Contains("window.__PRODUCT__", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new PageClassificationResult
                {
                    IsProductPage = true,
                    HasSpecifications = true,
                    DetectedCategory = category,
                    Confidence = 0.85d,
                    Reason = "Golden dataset classifier accepted the page as product-like."
                });
            }

            if (content.Contains("product-grid", StringComparison.OrdinalIgnoreCase)
                || content.Contains("comparison", StringComparison.OrdinalIgnoreCase)
                || content.Contains("review article", StringComparison.OrdinalIgnoreCase)
                || content.Contains("Best TVs 2024", StringComparison.OrdinalIgnoreCase)
                || content.Contains("support article", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new PageClassificationResult
                {
                    IsProductPage = false,
                    HasSpecifications = false,
                    Confidence = 0.1d,
                    Reason = "Golden dataset classifier rejected the page as non-product content."
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