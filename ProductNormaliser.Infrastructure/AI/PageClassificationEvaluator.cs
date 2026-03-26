using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProductNormaliser.Application.AI;
using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Infrastructure.Discovery;

namespace ProductNormaliser.Infrastructure.AI;

public sealed class PageClassificationEvaluator
{
    private readonly IPageClassificationService pageClassificationService;
    private readonly ProductPageClassifier productPageClassifier;
    private readonly ILogger<PageClassificationEvaluator> logger;

    public PageClassificationEvaluator(
        IPageClassificationService pageClassificationService,
        IStructuredDataExtractor structuredDataExtractor,
        ILogger<PageClassificationEvaluator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(pageClassificationService);
        ArgumentNullException.ThrowIfNull(structuredDataExtractor);

        this.pageClassificationService = pageClassificationService;
        productPageClassifier = new ProductPageClassifier(structuredDataExtractor, new DiscoveryLinkPolicy());
        this.logger = logger ?? NullLogger<PageClassificationEvaluator>.Instance;
    }

    public async Task<IReadOnlyList<PageClassificationEvaluationResult>> RunAsync(
        IReadOnlyList<PageClassificationEvaluationCase> evaluationCases,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evaluationCases);

        var results = new List<PageClassificationEvaluationResult>(evaluationCases.Count);
        foreach (var evaluationCase in evaluationCases)
        {
            var source = BuildSource(evaluationCase);
            var heuristic = productPageClassifier.Classify(source, evaluationCase.Url, evaluationCase.Content);
            var llm = await pageClassificationService.ClassifyAsync(evaluationCase.Content, evaluationCase.Category, cancellationToken);
            var llmAccepted = llm.IsProductPage;
            var heuristicAccepted = heuristic.IsProductPage;
            var finalDecision = DetermineFinalDecision(heuristicAccepted, heuristic.Confidence, llmAccepted, llm.Confidence);

            results.Add(new PageClassificationEvaluationResult
            {
                Name = evaluationCase.Name,
                ExpectedIsProduct = evaluationCase.ExpectedIsProduct,
                LlmIsProduct = llmAccepted,
                HeuristicIsProduct = heuristicAccepted,
                FinalDecision = finalDecision,
                Confidence = llm.Confidence,
                FalsePositive = finalDecision && !evaluationCase.ExpectedIsProduct,
                FalseNegative = !finalDecision && evaluationCase.ExpectedIsProduct,
                Reason = llm.Reason ?? heuristic.Reason
            });
        }

        var summary = Summarize(results);
        logger.LogInformation(
            "Page classification evaluation completed. Total: {Total}, LLM Accuracy: {LlmAccuracy}%, Heuristic Accuracy: {HeuristicAccuracy}%, Combined Accuracy: {CombinedAccuracy}%, False Positives: {FalsePositives}, False Negatives: {FalseNegatives}",
            summary.Total,
            summary.LlmAccuracy,
            summary.HeuristicAccuracy,
            summary.CombinedAccuracy,
            summary.FalsePositives,
            summary.FalseNegatives);

        return results;
    }

    public static PageClassificationEvaluationSummary Summarize(IReadOnlyList<PageClassificationEvaluationResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        if (results.Count == 0)
        {
            return new PageClassificationEvaluationSummary();
        }

        static double Accuracy(IEnumerable<PageClassificationEvaluationResult> source, Func<PageClassificationEvaluationResult, bool> selector)
        {
            var items = source.ToArray();
            if (items.Length == 0)
            {
                return 0d;
            }

            var correct = items.Count(item => selector(item) == item.ExpectedIsProduct);
            return Math.Round(correct * 100d / items.Length, 2, MidpointRounding.AwayFromZero);
        }

        return new PageClassificationEvaluationSummary
        {
            Total = results.Count,
            LlmAccuracy = Accuracy(results, item => item.LlmIsProduct),
            HeuristicAccuracy = Accuracy(results, item => item.HeuristicIsProduct),
            CombinedAccuracy = Accuracy(results, item => item.FinalDecision),
            FalsePositives = results.Count(item => item.FalsePositive),
            FalseNegatives = results.Count(item => item.FalseNegative)
        };
    }

    private static bool DetermineFinalDecision(bool heuristicAccepted, decimal heuristicConfidence, bool llmAccepted, double llmConfidence)
    {
        var heuristicsStrong = heuristicConfidence >= 0.70m;
        var heuristicsWeak = heuristicConfidence < 0.45m;
        var llmStrong = llmConfidence >= 0.70d;

        if (heuristicsStrong && llmAccepted && llmStrong)
        {
            return true;
        }

        if (heuristicsWeak && !llmAccepted)
        {
            return false;
        }

        if (heuristicAccepted != llmAccepted)
        {
            return false;
        }

        return heuristicAccepted && llmAccepted;
    }

    private static CrawlSource BuildSource(PageClassificationEvaluationCase evaluationCase)
    {
        var uri = new Uri(evaluationCase.Url, UriKind.Absolute);
        return new CrawlSource
        {
            Id = evaluationCase.Name,
            DisplayName = evaluationCase.Name,
            BaseUrl = uri.GetLeftPart(UriPartial.Authority) + "/",
            Host = uri.Host,
            IsEnabled = true,
            SupportedCategoryKeys = [string.IsNullOrWhiteSpace(evaluationCase.Category) ? "product" : evaluationCase.Category.Trim().ToLowerInvariant()],
            DiscoveryProfile = new SourceDiscoveryProfile
            {
                ProductUrlPatterns = ["/product/", "/products/", "/p/", "/item/", "/tv/"],
                ListingUrlPatterns = ["/category/", "/categories/", "/collections/", "/browse/", "/search"]
            },
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
    }
}