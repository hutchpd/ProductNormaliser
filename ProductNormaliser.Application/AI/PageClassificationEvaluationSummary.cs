namespace ProductNormaliser.Application.AI;

public sealed class PageClassificationEvaluationSummary
{
    public int Total { get; init; }
    public double LlmAccuracy { get; init; }
    public double LlmPrecision { get; init; }
    public double LlmRecall { get; init; }
    public double HeuristicAccuracy { get; init; }
    public double HeuristicPrecision { get; init; }
    public double HeuristicRecall { get; init; }
    public double CombinedAccuracy { get; init; }
    public double CombinedPrecision { get; init; }
    public double CombinedRecall { get; init; }
    public int FalsePositives { get; init; }
    public int FalseNegatives { get; init; }
}